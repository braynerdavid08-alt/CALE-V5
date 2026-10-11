using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Privacy;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Legal;

public sealed record MyAgeStatusDto(
    bool Required,
    bool Declared,
    bool IsMinor,
    string? GuardianStatus,
    string? GuardianName,
    string? GuardianRelationship,
    string? GuardianEmailMasked,
    DateTime? GuardianLinkExpiresAt,
    bool CanResend);

public sealed record DeclareAgeRequest(DateOnly? BirthDate, GuardianDto? Guardian);

public sealed record GuardianViewDto(
    string MinorFirstName,
    string GuardianName,
    string GuardianRelationship,
    string Status,
    bool LinkExpired,
    IReadOnlyList<LegalDocumentSummaryDto> Documents);

/// <param name="Signature">Nombre completo que escribe el representante como firma al autorizar.</param>
public sealed record GuardianDecisionRequest(string Decision, bool ConfirmIsGuardian, string? Signature = null);

public sealed record GuardianEventDto(
    string Action,
    string? DocumentVersions,
    string? IpAddress,
    string? Note,
    DateTime CreatedAt);

public sealed record AdminMinorDto(
    int UserId,
    string? UserName,
    string? UserEmail,
    bool UserActive,
    int AgeToday,
    string GuardianStatus,
    string? GuardianName,
    string? GuardianRelationship,
    string? GuardianEmail,
    DateTime? GuardianLinkExpiresAt,
    DateTime? GuardianDecidedAt,
    DateTime CreatedAt,
    IReadOnlyList<GuardianEventDto> Events);

/// <summary>
/// Fecha de nacimiento de los estudiantes y autorización del representante legal de los menores.
/// La autorización la da el representante desde un enlace personal enviado a su correo.
/// </summary>
public sealed class MinorConsentService
{
    public const string DecisionAuthorize = "authorize";
    public const string DecisionReject = "reject";
    public const string DecisionRevoke = "revoke";
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(5);
    private static readonly string[] GuardianDocuments = [LegalDocuments.Privacy, LegalDocuments.DataProcessing, LegalDocuments.Terms];

    private readonly CaleDbContext _db;
    private readonly IEmailSender _email;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly LegalService _legal;
    private readonly IConfiguration _config;
    private readonly ILogger<MinorConsentService> _logger;

    public MinorConsentService(
        CaleDbContext db,
        IEmailSender email,
        IRefreshTokenStore refreshTokens,
        LegalService legal,
        IConfiguration config,
        ILogger<MinorConsentService> logger)
    {
        _db = db;
        _email = email;
        _refreshTokens = refreshTokens;
        _legal = legal;
        _config = config;
        _logger = logger;
    }

    /// <summary>Valida antes de crear la cuenta del estudiante para no dejar cuentas a medias.</summary>
    public static void EnsureValid(DateOnly? birthDate, GuardianDto? guardian, string studentEmail, DateTime utcNow)
    {
        AgePolicy.EnsureValid(birthDate, utcNow);
        if (AgePolicy.IsMinor(birthDate!.Value, utcNow))
        {
            _ = NormalizeGuardian(guardian, studentEmail);
        }
    }

    /// <summary>Guarda la declaración y, si es menor, envía el enlace al representante.</summary>
    public async Task DeclareAsync(
        int userId,
        DateOnly? birthDate,
        GuardianDto? guardian,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var user = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");
        if (await _db.Set<AgeDeclaration>().AnyAsync(d => d.UserId == userId, ct))
        {
            throw new ConflictException(
                "Tu fecha de nacimiento ya está registrada. Si hay un error, escríbenos por Solicitudes.",
                "age_already_declared");
        }

        EnsureValid(birthDate, guardian, user.Email, now);

        var isMinor = AgePolicy.IsMinor(birthDate!.Value, now);
        var declaration = AgeDeclaration.Declare(userId, birthDate.Value, isMinor, now);
        if (isMinor)
        {
            var g = NormalizeGuardian(guardian, user.Email);
            declaration.SetGuardian(g.Name, g.Relationship, g.Email, now);
        }

        _db.Add(declaration);
        await _db.SaveChangesAsync(ct);
        _db.Add(GuardianConsentEvent.Record(declaration, GuardianEventActions.Declared, now, null, ipAddress, userAgent,
            isMinor ? "Menor de edad: se pide autorización al representante." : "Mayor de edad."));
        await _db.SaveChangesAsync(ct);

        if (isMinor)
        {
            await SendLinkAsync(declaration, user.Name, ct);
        }
    }

    public async Task<MyAgeStatusDto> StatusAsync(int userId, string role, CancellationToken ct)
    {
        if (Roles.Normalize(role) != Roles.Student)
        {
            return new MyAgeStatusDto(false, false, false, null, null, null, null, null, false);
        }

        var d = await _db.Set<AgeDeclaration>().AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (d is null)
        {
            return new MyAgeStatusDto(true, false, false, null, null, null, null, null, false);
        }

        var lastSent = await LastSentAsync(d.Id, ct);
        return new MyAgeStatusDto(
            true,
            true,
            d.IsMinorAtDeclaration,
            d.IsMinorAtDeclaration ? d.GuardianStatus : null,
            d.GuardianName,
            d.GuardianRelationship,
            d.GuardianEmail is null ? null : PersonalData.MaskEmail(d.GuardianEmail),
            d.GuardianLinkExpiresAt,
            d.AwaitsGuardian && (lastSent is null || lastSent <= DateTime.UtcNow - ResendCooldown));
    }

    /// <summary>El estudiante corrige los datos del representante (solo mientras está pendiente) y reenvía el enlace.</summary>
    public async Task<MyAgeStatusDto> UpdateGuardianAsync(int userId, string role, GuardianDto guardian, CancellationToken ct)
    {
        var d = await _db.Set<AgeDeclaration>().FirstOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw new NotFoundException("Primero indica tu fecha de nacimiento.", "age_not_declared");
        if (!d.AwaitsGuardian)
        {
            throw new ConflictException("La autorización ya fue decidida.", "guardian_already_decided");
        }

        var lastSent = await LastSentAsync(d.Id, ct);
        if (lastSent is { } sent && sent > DateTime.UtcNow - ResendCooldown)
        {
            throw new DomainException("Espera unos minutos antes de reenviar el correo.", 429, "guardian_resend_too_soon");
        }

        var user = await _db.Set<User>().AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var g = NormalizeGuardian(guardian, user.Email);
        var now = DateTime.UtcNow;
        var changed = g.Email != d.GuardianEmail || g.Name != d.GuardianName || g.Relationship != d.GuardianRelationship;
        d.SetGuardian(g.Name, g.Relationship, g.Email, now);
        if (changed)
        {
            _db.Add(GuardianConsentEvent.Record(d, GuardianEventActions.GuardianUpdated, now));
        }

        await _db.SaveChangesAsync(ct);
        await SendLinkAsync(d, user.Name, ct);
        return await StatusAsync(userId, role, ct);
    }

    public async Task<GuardianViewDto> GuardianViewAsync(string token, CancellationToken ct)
    {
        var d = await FindByTokenAsync(token, ct);
        var name = await _db.Set<User>().AsNoTracking().Where(u => u.Id == d.UserId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        var documents = (await _legal.ListPublicAsync(ct)).Where(x => GuardianDocuments.Contains(x.Key)).ToList();
        return new GuardianViewDto(
            FirstName(name),
            d.GuardianName ?? "",
            d.GuardianRelationship ?? "",
            d.GuardianStatus,
            d.GuardianLinkExpiresAt is { } expires && expires <= DateTime.UtcNow,
            documents);
    }

    public async Task<GuardianViewDto> DecideAsync(
        string token,
        GuardianDecisionRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct)
    {
        var d = await FindByTokenAsync(token, ct);
        var now = DateTime.UtcNow;
        var decision = request.Decision?.Trim().ToLowerInvariant();
        string action;
        string? signature = null;
        switch (decision)
        {
            case DecisionAuthorize:
                if (!request.ConfirmIsGuardian)
                {
                    throw new DomainException(
                        "Confirma que eres el representante legal del menor para autorizar.",
                        400,
                        "guardian_confirmation_required");
                }

                signature = request.Signature?.Trim() ?? "";
                if (signature.Length < AgeDeclaration.GuardianNameMin || signature.Length > AgeDeclaration.GuardianNameMax)
                {
                    throw new DomainException(
                        "Escribe tu nombre completo como firma para autorizar.",
                        400,
                        "guardian_signature_required");
                }

                d.Confirm(now);
                action = GuardianEventActions.Confirmed;
                break;
            case DecisionReject:
                d.Reject(now);
                action = GuardianEventActions.Rejected;
                break;
            case DecisionRevoke:
                d.Revoke(now);
                action = GuardianEventActions.Revoked;
                break;
            default:
                throw new DomainException("Decisión no válida.", 400, "guardian_decision_invalid");
        }

        _db.Add(GuardianConsentEvent.Record(d, action, now, await CurrentVersionsAsync(ct), ipAddress, userAgent,
            action == GuardianEventActions.Confirmed
                ? $"Declaró ser el representante legal y autorizó. Firma: {signature}"
                : null));

        if (action != GuardianEventActions.Confirmed)
        {
            await SuspendMinorAsync(d.UserId, action, now, ct);
        }

        await _db.SaveChangesAsync(ct);
        if (action != GuardianEventActions.Confirmed)
        {
            await _refreshTokens.RevokeAllForUserAsync(d.UserId, ct);
        }

        _logger.LogWarning("Audit: guardian decision {Action} for user {UserId}", action, d.UserId);
        return await GuardianViewAsync(token, ct);
    }

    public async Task<IReadOnlyList<AdminMinorDto>> AdminListAsync(CancellationToken ct)
    {
        var declarations = await _db.Set<AgeDeclaration>().AsNoTracking()
            .Where(d => d.IsMinorAtDeclaration)
            .OrderByDescending(d => d.CreatedAt)
            .Take(500)
            .ToListAsync(ct);
        var userIds = declarations.Select(d => d.UserId).ToList();
        var users = await _db.Set<User>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email, u.IsActive })
            .ToDictionaryAsync(u => u.Id, ct);
        var events = (await _db.Set<GuardianConsentEvent>().AsNoTracking()
                .Where(e => userIds.Contains(e.UserId))
                .ToListAsync(ct))
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .ToLookup(e => e.UserId);
        var today = AgePolicy.TodayInColombia(DateTime.UtcNow);

        return declarations.Select(d =>
        {
            users.TryGetValue(d.UserId, out var u);
            return new AdminMinorDto(
                d.UserId, u?.Name, u?.Email, u?.IsActive ?? false,
                AgePolicy.AgeOn(d.BirthDate, today),
                d.GuardianStatus, d.GuardianName, d.GuardianRelationship, d.GuardianEmail,
                d.GuardianLinkExpiresAt, d.GuardianDecidedAt, d.CreatedAt,
                events[d.UserId].Select(e => new GuardianEventDto(e.Action, e.DocumentVersions, e.IpAddress, e.Note, e.CreatedAt)).ToList());
        }).ToList();
    }

    public async Task AdminResendAsync(int userId, CancellationToken ct)
    {
        var d = await _db.Set<AgeDeclaration>().FirstOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw new NotFoundException("El estudiante no tiene fecha de nacimiento registrada.", "age_not_declared");
        if (!d.AwaitsGuardian)
        {
            throw new ConflictException("La autorización ya fue decidida.", "guardian_already_decided");
        }

        var name = await _db.Set<User>().AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstAsync(ct);
        await SendLinkAsync(d, name, ct);
    }

    // ---- Internos ----

    private static (string Name, string Relationship, string Email) NormalizeGuardian(GuardianDto? guardian, string studentEmail)
    {
        var name = guardian?.Name?.Trim() ?? "";
        if (name.Length < AgeDeclaration.GuardianNameMin || name.Length > AgeDeclaration.GuardianNameMax)
        {
            throw new DomainException(
                "Como eres menor de edad, escribe el nombre completo de tu madre, padre o tutor legal.",
                400,
                "guardian_required");
        }

        var relationship = GuardianRelationships.Find(guardian?.Relationship)
            ?? throw new DomainException("Indica si es tu madre, padre o tutor legal.", 400, "guardian_relationship_invalid");

        string email;
        try
        {
            email = EmailAddress.NormalizeForRegistration(guardian?.Email);
        }
        catch (DomainException)
        {
            throw new DomainException("Escribe un correo válido de tu representante legal.", 400, "guardian_email_invalid");
        }

        if (email.Length > AgeDeclaration.GuardianEmailMax)
        {
            throw new DomainException("Escribe un correo válido de tu representante legal.", 400, "guardian_email_invalid");
        }

        if (string.Equals(email, EmailAddress.Normalize(studentEmail), StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                "El correo del representante debe ser distinto al tuyo.",
                400,
                "guardian_email_same");
        }

        return (name, relationship, email);
    }

    private async Task SendLinkAsync(AgeDeclaration d, string minorName, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var token = d.IssueLink(now);
        await _db.SaveChangesAsync(ct);

        // Sin correo real configurado no se envía: el enlace es una credencial y no debe quedar en los registros.
        if (!_email.IsConfigured || d.GuardianEmail is null)
        {
            _db.Add(GuardianConsentEvent.Record(d, GuardianEventActions.EmailNotSent, now, note: "Correo no configurado."));
            await _db.SaveChangesAsync(ct);
            return;
        }

        var link = $"{PublicBaseUrl()}/autorizacion-menor/{token}";
        var body =
            $"Hola, {d.GuardianName}.\n\n" +
            $"{FirstName(minorName)} creó una cuenta de estudiante en Luz Verde, una plataforma para prepararse para el examen teórico de conducción, " +
            $"y te indicó como su {d.GuardianRelationship?.ToLowerInvariant()}.\n\n" +
            "Como es menor de edad, te pedimos leer cómo tratamos sus datos y, si estás de acuerdo, firmar la autorización escribiendo tu nombre:\n\n" +
            $"{link}\n\n" +
            $"El enlace vence el {d.GuardianLinkExpiresAt!.Value.AddHours(-5):dd/MM/yyyy}. Con el mismo enlace podrás revocar la autorización cuando quieras.\n" +
            "Si no conoces a este estudiante, ignora este correo o entra al enlace y elige \"No autorizo\".";
        try
        {
            await _email.SendAsync(d.GuardianEmail, "Autoriza la cuenta de un menor en Luz Verde", body, ct);
            _db.Add(GuardianConsentEvent.Record(d, GuardianEventActions.EmailSent, now, note: $"Enviado a {PersonalData.MaskEmail(d.GuardianEmail)}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Guardian consent email failed for user {UserId}", d.UserId);
            _db.Add(GuardianConsentEvent.Record(d, GuardianEventActions.EmailNotSent, now, note: "Falló el envío del correo."));
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SuspendMinorAsync(int userId, string action, DateTime now, CancellationToken ct)
    {
        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var reason = action == GuardianEventActions.Revoked
            ? "Tu representante legal revocó la autorización para usar Luz Verde."
            : "Tu representante legal no autorizó el uso de Luz Verde.";
        user.Deactivate();
        _db.Add(AccountStatusEvent.Suspend(user.Id, reason, "Decisión registrada desde el enlace del representante legal.", null, null, now));
    }

    private async Task<AgeDeclaration> FindByTokenAsync(string token, CancellationToken ct)
    {
        var hash = string.IsNullOrWhiteSpace(token) || token.Length > 100 ? "" : AgeDeclaration.HashToken(token.Trim());
        return await _db.Set<AgeDeclaration>().FirstOrDefaultAsync(d => hash != "" && d.GuardianTokenHash == hash, ct)
            ?? throw new NotFoundException("El enlace no es válido o fue reemplazado por uno más reciente.", "guardian_link_invalid");
    }

    private async Task<DateTime?> LastSentAsync(int declarationId, CancellationToken ct) =>
        (await _db.Set<GuardianConsentEvent>().AsNoTracking()
            .Where(e => e.AgeDeclarationId == declarationId
                && (e.Action == GuardianEventActions.EmailSent || e.Action == GuardianEventActions.EmailNotSent))
            .Select(e => e.CreatedAt)
            .ToListAsync(ct))
        .Cast<DateTime?>()
        .Max();

    private async Task<string> CurrentVersionsAsync(CancellationToken ct) =>
        string.Join(';', (await _legal.ListPublicAsync(ct))
            .Where(x => GuardianDocuments.Contains(x.Key) && x.Version is not null)
            .Select(x => $"{x.Key}@{x.Version}"));

    // No se usa la cabecera Host de la petición: el enlace va por correo y no debe poder falsearse.
    private string PublicBaseUrl()
    {
        var configured = _config["PublicAppUrl"];
        return string.IsNullOrWhiteSpace(configured) ? "https://micale.onrender.com" : configured.TrimEnd('/');
    }

    private static string FirstName(string? name)
    {
        var first = name?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "Un estudiante" : first;
    }
}
