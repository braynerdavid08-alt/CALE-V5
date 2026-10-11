using System.Net.Mail;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Privacy;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Legal;

public sealed record PqrKindDto(string Key, string Label, bool IsDataRight, int DeadlineDays);

public sealed record CreatePqrRequest(string Kind, string? Name, string? Email, string Subject, string Message);

public sealed record PqrCreatedDto(string Radicado, string TrackingCode, DateTime DueAt, bool EmailSent);

public sealed record PqrEventDto(string Action, string? FromStatus, string? ToStatus, string? Note, bool VisibleToRequester, DateTime CreatedAt);

public sealed record PqrDto(
    int Id,
    string Radicado,
    string Kind,
    string KindLabel,
    string Status,
    string Subject,
    string Message,
    string? Response,
    DateTime DueAt,
    bool Extended,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    IReadOnlyList<PqrEventDto> Events);

public sealed record AdminPqrDto(
    int Id,
    string Radicado,
    string Kind,
    string KindLabel,
    bool IsDataRight,
    string Status,
    int? UserId,
    string? UserRole,
    bool? UserActive,
    string ContactName,
    string ContactEmail,
    string Subject,
    string Message,
    string? Response,
    DateTime DueAt,
    bool Extended,
    bool Overdue,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    IReadOnlyList<PqrEventDto> Events);

public sealed record UpdatePqrRequest(string Status, string? Response, string? InternalNote);

public sealed record ExtendPqrRequest(string Reason);

public sealed record PqrCountsDto(int Open, int Overdue, int DueSoon);

/// <summary>
/// Peticiones, quejas, reclamos y derechos del titular (Ley 1581). Se pueden enviar sin iniciar
/// sesión (p. ej. una cuenta suspendida). Nunca se borran: cada cambio queda en el historial.
/// </summary>
public sealed class PqrService
{
    private const int MaxOpenPerRequester = 5;

    private readonly CaleDbContext _db;
    private readonly IEmailSender _email;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<PqrService> _logger;

    public PqrService(CaleDbContext db, IEmailSender email, INotificationPublisher notifications, ILogger<PqrService> logger)
    {
        _db = db;
        _email = email;
        _notifications = notifications;
        _logger = logger;
    }

    public static IReadOnlyList<PqrKindDto> Kinds() =>
        PqrKinds.All.Select(k => new PqrKindDto(k.Key, k.Label, k.IsDataRight, PqrDeadlines.For(k).Days)).ToList();

    public async Task<PqrCreatedDto> CreateAsync(int? userId, CreatePqrRequest request, CancellationToken ct)
    {
        var kind = PqrKinds.Find(request.Kind)
            ?? throw new DomainException("Elige el tipo de solicitud.", 400, "pqr_kind_invalid");

        string name;
        string email;
        if (userId is { } id)
        {
            var user = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
                ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");
            name = user.Name;
            email = user.Email;
        }
        else
        {
            name = Required(request.Name, 3, PqrRequest.NameMax, "Escribe tu nombre completo.", "pqr_name_required");
            email = ValidEmail(request.Email);
        }

        var subject = Required(request.Subject, 5, PqrRequest.SubjectMax, "Escribe un asunto (5 a 200 caracteres).", "pqr_subject_required");
        var message = Required(
            request.Message, PqrRequest.MessageMin, PqrRequest.MessageMax,
            "Describe tu solicitud (20 a 4000 caracteres).", "pqr_message_required");

        var open = await _db.Set<PqrRequest>().CountAsync(
            r => (userId != null ? r.UserId == userId : r.ContactEmail == email)
                && r.Status != PqrStatuses.Answered
                && r.Status != PqrStatuses.Rejected,
            ct);
        if (open >= MaxOpenPerRequester)
        {
            throw new DomainException(
                "Ya tienes 5 solicitudes abiertas. Espera la respuesta antes de enviar otra.",
                429,
                "pqr_too_many_open");
        }

        var now = DateTime.UtcNow;
        var (entity, code) = PqrRequest.Create(kind, userId, name, email, subject, message, now);
        _db.Add(entity);
        await _db.SaveChangesAsync(ct);
        _db.Add(PqrEvent.Record(entity, PqrEventActions.Created, now, userId, toStatus: PqrStatuses.Pending));
        await _db.SaveChangesAsync(ct);

        await NotifyAdminsAsync(entity, kind, ct);
        var sent = await SendEmailAsync(
            entity.ContactEmail,
            $"Recibimos tu solicitud {entity.Radicado}",
            $"Hola, {entity.ContactName}.\n\n" +
            $"Recibimos tu solicitud \"{entity.Subject}\" ({kind.Label}).\n" +
            $"Radicado: {entity.Radicado}\nCódigo de seguimiento: {code}\n" +
            $"Fecha máxima de respuesta: {ColombiaDate(entity.DueAt)}.\n\n" +
            "Guarda el código: con él puedes consultar el estado en Luz Verde, en la sección PQR.",
            ct);
        return new PqrCreatedDto(entity.Radicado, code, entity.DueAt, sent);
    }

    public async Task<IReadOnlyList<PqrDto>> MineAsync(int userId, CancellationToken ct)
    {
        var rows = await _db.Set<PqrRequest>().AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        var events = await EventsAsync(rows.Select(r => r.Id).ToList(), requesterView: true, ct);
        return rows.Select(r => ToDto(r, events[r.Id])).ToList();
    }

    public async Task<PqrDto> TrackAsync(string code, CancellationToken ct)
    {
        var hash = string.IsNullOrWhiteSpace(code) || code.Length > 64 ? "" : PqrRequest.HashCode(code);
        var entity = await _db.Set<PqrRequest>().AsNoTracking()
            .FirstOrDefaultAsync(r => hash != "" && r.TrackingCodeHash == hash, ct)
            ?? throw new NotFoundException("No encontramos una solicitud con ese código.", "pqr_not_found");
        var events = await EventsAsync([entity.Id], requesterView: true, ct);
        return ToDto(entity, events[entity.Id]);
    }

    // ---- Administración ----

    public async Task<IReadOnlyList<AdminPqrDto>> AdminListAsync(string? status, string? kind, CancellationToken ct)
    {
        var query = _db.Set<PqrRequest>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = status == "open"
                ? query.Where(r => r.Status != PqrStatuses.Answered && r.Status != PqrStatuses.Rejected)
                : query.Where(r => r.Status == status);
        }

        if (PqrKinds.Find(kind) is { } k)
        {
            query = query.Where(r => r.Kind == k.Key);
        }

        var rows = await query.OrderBy(r => r.DueAt).Take(300).ToListAsync(ct);
        var events = await EventsAsync(rows.Select(r => r.Id).ToList(), requesterView: false, ct);
        var userIds = rows.Where(r => r.UserId != null).Select(r => r.UserId!.Value).Distinct().ToList();
        var users = await _db.Set<User>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Role, u.IsActive })
            .ToDictionaryAsync(u => u.Id, ct);
        var now = DateTime.UtcNow;
        return rows.Select(r =>
        {
            var info = PqrKinds.Find(r.Kind);
            var user = r.UserId is { } uid && users.TryGetValue(uid, out var u) ? u : null;
            return new AdminPqrDto(
                r.Id, r.Radicado, r.Kind, info?.Label ?? r.Kind, info?.IsDataRight ?? false, r.Status,
                r.UserId, user is null ? null : Roles.Normalize(user.Role), user?.IsActive,
                r.ContactName, r.ContactEmail, r.Subject, r.Message, r.Response, r.DueAt, r.Extended,
                !r.IsClosed && r.DueAt < now, r.CreatedAt, r.ClosedAt, events[r.Id].ToList());
        }).ToList();
    }

    public async Task<PqrCountsDto> CountsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var soon = PqrDeadlines.AddBusinessDays(now, 3);
        var open = await _db.Set<PqrRequest>().AsNoTracking()
            .Where(r => r.Status != PqrStatuses.Answered && r.Status != PqrStatuses.Rejected)
            .Select(r => r.DueAt)
            .ToListAsync(ct);
        return new PqrCountsDto(open.Count, open.Count(d => d < now), open.Count(d => d >= now && d <= soon));
    }

    public async Task UpdateAsync(int id, int adminId, UpdatePqrRequest request, CancellationToken ct)
    {
        var entity = await FindAsync(id, ct);
        var now = DateTime.UtcNow;
        var from = entity.Status;
        var status = request.Status?.Trim().ToUpperInvariant() ?? "";

        if (status != from)
        {
            entity.ChangeStatus(status, request.Response, adminId, now);
            _db.Add(PqrEvent.Record(entity, PqrEventActions.StatusChanged, now, adminId, from, status,
                entity.IsClosed ? entity.Response : null));
        }

        if (!string.IsNullOrWhiteSpace(request.InternalNote))
        {
            _db.Add(PqrEvent.Record(entity, PqrEventActions.Note, now, adminId, note: request.InternalNote, visibleToRequester: false));
        }

        await _db.SaveChangesAsync(ct);
        if (status != from)
        {
            await NotifyRequesterAsync(entity, ct);
        }
    }

    public async Task ExtendAsync(int id, int adminId, ExtendPqrRequest request, CancellationToken ct)
    {
        var reason = Required(request.Reason, 10, 500, "Explica el motivo de la prórroga (mínimo 10 caracteres).", "pqr_extension_reason_required");
        var entity = await FindAsync(id, ct);
        var now = DateTime.UtcNow;
        entity.Extend(now);
        _db.Add(PqrEvent.Record(entity, PqrEventActions.Extended, now, adminId,
            note: $"Plazo prorrogado hasta el {ColombiaDate(entity.DueAt)}. Motivo: {reason}"));
        await _db.SaveChangesAsync(ct);
        await SendEmailAsync(
            entity.ContactEmail,
            $"Prórroga de tu solicitud {entity.Radicado}",
            $"Hola, {entity.ContactName}.\n\nNecesitamos más tiempo para responder tu solicitud {entity.Radicado}.\n" +
            $"Motivo: {reason}\nNueva fecha máxima de respuesta: {ColombiaDate(entity.DueAt)}.",
            ct);
    }

    // ---- Internos ----

    private async Task<PqrRequest> FindAsync(int id, CancellationToken ct) =>
        await _db.Set<PqrRequest>().FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException("Solicitud no encontrada.", "pqr_not_found");

    private async Task<ILookup<int, PqrEventDto>> EventsAsync(List<int> ids, bool requesterView, CancellationToken ct)
    {
        var rows = await _db.Set<PqrEvent>().AsNoTracking()
            .Where(e => ids.Contains(e.PqrRequestId) && (!requesterView || e.VisibleToRequester))
            .ToListAsync(ct);
        return rows
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToLookup(e => e.PqrRequestId, e => new PqrEventDto(e.Action, e.FromStatus, e.ToStatus, e.Note, e.VisibleToRequester, e.CreatedAt));
    }

    private static PqrDto ToDto(PqrRequest r, IEnumerable<PqrEventDto> events) =>
        new(r.Id, r.Radicado, r.Kind, PqrKinds.Find(r.Kind)?.Label ?? r.Kind, r.Status, r.Subject, r.Message,
            r.Response, r.DueAt, r.Extended, r.CreatedAt, r.ClosedAt, events.ToList());

    private async Task NotifyAdminsAsync(PqrRequest entity, PqrKindInfo kind, CancellationToken ct)
    {
        try
        {
            var adminIds = await _db.Set<User>().AsNoTracking()
                .Where(u => u.IsActive && u.Role == Roles.Admin)
                .Select(u => u.Id)
                .ToListAsync(ct);
            if (adminIds.Count > 0)
            {
                await _notifications.NotifyUsersAsync(
                    adminIds,
                    new NotificationDraft(
                        $"Nueva PQR {entity.Radicado}",
                        $"{kind.Label}: «{entity.Subject}». Vence el {ColombiaDate(entity.DueAt)}.",
                        NotificationTypes.Admin,
                        RelatedEntity: "pqr",
                        RelatedId: entity.Id,
                        Link: "/admin/pqr"),
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PQR admin notification failed for {Id}", entity.Id);
        }
    }

    private async Task NotifyRequesterAsync(PqrRequest entity, CancellationToken ct)
    {
        var label = entity.Status switch
        {
            PqrStatuses.InReview => "está en revisión",
            PqrStatuses.Answered => "fue respondida",
            PqrStatuses.Rejected => "fue rechazada con justificación",
            PqrStatuses.Escalated => "fue escalada para una revisión adicional",
            _ => "cambió de estado"
        };
        var text = $"Tu solicitud {entity.Radicado} («{entity.Subject}») {label}.";
        if (entity.UserId is { } userId)
        {
            try
            {
                await _notifications.NotifyUsersAsync(
                    [userId],
                    new NotificationDraft("Actualización de tu PQR", text, NotificationTypes.System,
                        RelatedEntity: "pqr", RelatedId: entity.Id, Link: "/mis-pqr"),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PQR requester notification failed for {Id}", entity.Id);
            }
        }

        var body = $"Hola, {entity.ContactName}.\n\n{text}";
        if (entity.IsClosed && entity.Response is not null)
        {
            body += $"\n\nRespuesta:\n{entity.Response}";
        }

        await SendEmailAsync(entity.ContactEmail, $"Actualización de tu solicitud {entity.Radicado}", body, ct);
    }

    private async Task<bool> SendEmailAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (!_email.IsConfigured)
        {
            return false;
        }

        try
        {
            await _email.SendAsync(to, subject, body, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PQR email to {To} failed", PersonalData.MaskEmail(to));
            return false;
        }
    }

    private static string Required(string? value, int min, int max, string message, string code)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length < min || trimmed.Length > max)
        {
            throw new DomainException(message, 400, code);
        }

        return trimmed;
    }

    private static string ValidEmail(string? value)
    {
        var email = value?.Trim().ToLowerInvariant() ?? "";
        try
        {
            if (email.Length is > 0 and <= PqrRequest.EmailMax && new MailAddress(email).Address == email && email.Contains('.'))
            {
                return email;
            }
        }
        catch (FormatException)
        {
        }

        throw new DomainException("Escribe un correo válido para enviarte la respuesta.", 400, "pqr_email_invalid");
    }

    private static string ColombiaDate(DateTime utc) => utc.AddHours(-5).ToString("dd/MM/yyyy");
}
