using System.Globalization;
using System.Text;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Legal;

public sealed record LegalDocumentSummaryDto(
    string Key,
    string Title,
    string? Version,
    DateTime? PublishedAt,
    bool IsDraft);

public sealed record LegalDocumentDto(
    string Key,
    string Title,
    string Version,
    string Content,
    string ContentHash,
    DateTime? PublishedAt,
    bool IsDraft);

public sealed record LegalRequirementDto(
    string Key,
    string Title,
    string Action,
    string Status,
    string? CurrentVersion,
    string? AcceptedVersion,
    DateTime? AcceptedAt);

public sealed record MyLegalStatusDto(bool AllAccepted, IReadOnlyList<LegalRequirementDto> Documents);

public sealed record LegalAcceptanceDto(
    int Id,
    int UserId,
    string? UserName,
    string? UserEmail,
    string DocumentKey,
    string Version,
    string Action,
    string Method,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt);

public sealed record LegalVersionDto(
    int Id,
    string DocumentKey,
    string Version,
    string Title,
    string Content,
    string ContentHash,
    string Status,
    bool RequiresReacceptance,
    bool HasPlaceholders,
    string? ChangeSummary,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    int Acceptances);

public sealed record SaveLegalVersionRequest(
    string DocumentKey,
    string Version,
    string Title,
    string Content,
    string? ChangeSummary,
    bool RequiresReacceptance);

public sealed record LegalSummaryDto(
    string Key,
    string Title,
    string? CurrentVersion,
    bool IsDraft,
    int Accepted,
    int Outdated,
    int NotVerified);

public sealed class LegalService
{
    public const string ConsentRequiredCode = "legal_consent_required";

    private readonly CaleDbContext _db;

    public LegalService(CaleDbContext db) => _db = db;

    public async Task<IReadOnlyList<LegalDocumentSummaryDto>> ListPublicAsync(CancellationToken ct)
    {
        var current = await CurrentVersionsAsync(ct);
        return LegalDocuments.All
            .Select(d => current.TryGetValue(d.Key, out var v)
                ? new LegalDocumentSummaryDto(d.Key, v.Title, v.Version, v.PublishedAt, v.HasPlaceholders)
                : new LegalDocumentSummaryDto(d.Key, d.Title, null, null, true))
            .ToList();
    }

    public async Task<LegalDocumentDto> GetPublicAsync(string key, CancellationToken ct)
    {
        var doc = LegalDocuments.Find(key)
            ?? throw new NotFoundException("Documento no encontrado.", "legal_document_unknown");
        var version = await _db.Set<LegalDocumentVersion>().AsNoTracking()
            .Where(v => v.DocumentKey == doc.Key && v.Status == LegalVersionStatuses.Published)
            .OrderByDescending(v => v.PublishedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Documento aún no publicado.", "legal_document_unpublished");
        return new LegalDocumentDto(
            doc.Key, version.Title, version.Version, version.Content, version.ContentHash,
            version.PublishedAt, version.HasPlaceholders);
    }

    /// <summary>Valida las casillas del registro antes de crear la cuenta.</summary>
    public static void EnsureConsent(LegalConsentDto? consent, string role)
    {
        foreach (var doc in LegalDocuments.RequiredFor(role))
        {
            if (consent is null || !Checked(consent, doc.Key))
            {
                throw new DomainException(
                    "Debes aceptar los términos, leer el aviso de privacidad y autorizar el tratamiento de datos para continuar.",
                    400,
                    ConsentRequiredCode);
            }
        }
    }

    public async Task RecordAsync(
        int userId,
        string role,
        LegalConsentDto consent,
        string method,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct)
    {
        var current = await CurrentVersionsAsync(ct);
        var status = await StatusAsync(userId, role, ct);
        var now = DateTime.UtcNow;
        foreach (var requirement in status.Documents.Where(d => d.Status != LegalAcceptanceStatuses.Accepted))
        {
            if (!Checked(consent, requirement.Key) || !current.TryGetValue(requirement.Key, out var version))
            {
                continue;
            }

            _db.Add(UserLegalAcceptance.Record(userId, version, requirement.Action, method, ipAddress, userAgent, now));
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<MyLegalStatusDto> AcceptAsync(
        int userId,
        string role,
        LegalConsentDto consent,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct)
    {
        var status = await StatusAsync(userId, role, ct);
        if (status.Documents.Any(d => d.Status != LegalAcceptanceStatuses.Accepted && !Checked(consent, d.Key)))
        {
            throw new DomainException(
                "Marca cada casilla pendiente para continuar.",
                400,
                ConsentRequiredCode);
        }

        await RecordAsync(userId, role, consent, LegalAcceptanceMethods.Prompt, ipAddress, userAgent, ct);
        return await StatusAsync(userId, role, ct);
    }

    public async Task<MyLegalStatusDto> StatusAsync(int userId, string role, CancellationToken ct)
    {
        var required = LegalDocuments.RequiredFor(role);
        if (required.Count == 0)
        {
            return new MyLegalStatusDto(true, []);
        }

        var keys = required.Select(d => d.Key).ToList();
        var versions = await PublishedHistoryAsync(keys, ct);
        var acceptances = await _db.Set<UserLegalAcceptance>().AsNoTracking()
            .Where(a => a.UserId == userId && keys.Contains(a.DocumentKey))
            .ToListAsync(ct);

        var documents = required
            .Select(doc => Requirement(doc, versions, acceptances))
            .ToList();
        return new MyLegalStatusDto(documents.All(d => d.Status == LegalAcceptanceStatuses.Accepted), documents);
    }

    public async Task<int?> FindUserIdByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = Cale.BuildingBlocks.Domain.Validation.EmailAddress.Normalize(email);
        return await _db.Set<User>().AsNoTracking()
            .Where(u => u.Email == normalized)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<LegalAcceptanceDto>> HistoryAsync(int userId, CancellationToken ct) =>
        await AcceptanceQuery(_db.Set<UserLegalAcceptance>().Where(a => a.UserId == userId), 200, ct);

    // ---- Administración ----

    public async Task<IReadOnlyList<LegalVersionDto>> ListVersionsAsync(CancellationToken ct)
    {
        var counts = await _db.Set<UserLegalAcceptance>().AsNoTracking()
            .GroupBy(a => a.DocumentVersionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var versions = await _db.Set<LegalDocumentVersion>().AsNoTracking()
            .OrderBy(v => v.DocumentKey)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync(ct);
        return versions.Select(v => ToDto(v, counts.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<LegalVersionDto> CreateDraftAsync(SaveLegalVersionRequest request, int actorUserId, CancellationToken ct)
    {
        var draft = LegalDocumentVersion.CreateDraft(
            request.DocumentKey, request.Version, request.Title, request.Content,
            request.ChangeSummary, request.RequiresReacceptance, actorUserId, DateTime.UtcNow);
        await EnsureVersionFreeAsync(draft.DocumentKey, draft.Version, null, ct);
        _db.Add(draft);
        await _db.SaveChangesAsync(ct);
        return ToDto(draft, 0);
    }

    public async Task<LegalVersionDto> UpdateDraftAsync(int id, SaveLegalVersionRequest request, CancellationToken ct)
    {
        var draft = await FindVersionAsync(id, ct);
        draft.UpdateDraft(request.Version, request.Title, request.Content, request.ChangeSummary, request.RequiresReacceptance);
        await EnsureVersionFreeAsync(draft.DocumentKey, draft.Version, draft.Id, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(draft, 0);
    }

    public async Task<LegalVersionDto> PublishAsync(int id, int actorUserId, CancellationToken ct)
    {
        var draft = await FindVersionAsync(id, ct);
        var previous = await _db.Set<LegalDocumentVersion>()
            .Where(v => v.DocumentKey == draft.DocumentKey && v.Status == LegalVersionStatuses.Published)
            .ToListAsync(ct);
        draft.Publish(actorUserId, DateTime.UtcNow);
        foreach (var old in previous)
        {
            old.Retire();
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(draft, 0);
    }

    public async Task<IReadOnlyList<LegalSummaryDto>> SummaryAsync(CancellationToken ct)
    {
        var current = await CurrentVersionsAsync(ct);
        var keys = LegalDocuments.All.Select(d => d.Key).ToList();
        var versions = await PublishedHistoryAsync(keys, ct);
        var users = await _db.Set<User>().AsNoTracking()
            .Select(u => new { u.Id, u.Role })
            .ToListAsync(ct);
        var acceptances = (await _db.Set<UserLegalAcceptance>().AsNoTracking().ToListAsync(ct))
            .ToLookup(a => a.UserId);

        var result = new List<LegalSummaryDto>();
        foreach (var doc in LegalDocuments.All)
        {
            int accepted = 0, outdated = 0, notVerified = 0;
            foreach (var user in users.Where(u => doc.RequiredForRoles.Contains(Roles.Normalize(u.Role))))
            {
                switch (Requirement(doc, versions, acceptances[user.Id].ToList()).Status)
                {
                    case LegalAcceptanceStatuses.Accepted: accepted++; break;
                    case LegalAcceptanceStatuses.Outdated: outdated++; break;
                    default: notVerified++; break;
                }
            }

            current.TryGetValue(doc.Key, out var version);
            result.Add(new LegalSummaryDto(
                doc.Key, doc.Title, version?.Version, version?.HasPlaceholders ?? true, accepted, outdated, notVerified));
        }

        return result;
    }

    public async Task<IReadOnlyList<LegalAcceptanceDto>> AcceptancesAsync(string? documentKey, int take, CancellationToken ct)
    {
        var query = _db.Set<UserLegalAcceptance>().AsQueryable();
        if (LegalDocuments.Find(documentKey) is { } doc)
        {
            query = query.Where(a => a.DocumentKey == doc.Key);
        }

        return await AcceptanceQuery(query, Math.Clamp(take, 1, 5000), ct);
    }

    public async Task<string> AcceptancesCsvAsync(CancellationToken ct)
    {
        var rows = await AcceptancesAsync(null, 5000, ct);
        var sb = new StringBuilder();
        sb.AppendLine("id,usuario_id,nombre,correo,documento,version,accion,metodo,ip,navegador,fecha_utc");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                r.Id.ToString(CultureInfo.InvariantCulture),
                r.UserId.ToString(CultureInfo.InvariantCulture),
                Csv(r.UserName), Csv(r.UserEmail), Csv(r.DocumentKey), Csv(r.Version),
                Csv(r.Action), Csv(r.Method), Csv(r.IpAddress), Csv(r.UserAgent),
                r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
        }

        return sb.ToString();
    }

    // ---- Internos ----

    private static bool Checked(LegalConsentDto consent, string key) => key switch
    {
        LegalDocuments.Terms => consent.AcceptTerms,
        LegalDocuments.Privacy => consent.AcknowledgePrivacy,
        LegalDocuments.DataProcessing => consent.AuthorizeDataProcessing,
        LegalDocuments.SchoolTerms => consent.AcceptSchoolTerms,
        _ => true
    };

    private static LegalRequirementDto Requirement(
        LegalDocumentInfo doc,
        IReadOnlyDictionary<string, List<LegalDocumentVersion>> versions,
        IReadOnlyCollection<UserLegalAcceptance> acceptances)
    {
        var history = versions.GetValueOrDefault(doc.Key) ?? [];
        var current = history.FirstOrDefault(v => v.Status == LegalVersionStatuses.Published);
        if (current is null)
        {
            return new LegalRequirementDto(doc.Key, doc.Title, doc.RequiredAction, LegalAcceptanceStatuses.Accepted, null, null, null);
        }

        // La versión más reciente que exige nueva aceptación marca el mínimo válido.
        var baseline = history.FirstOrDefault(v => v.RequiresReacceptance) ?? history[^1];
        var publishedAt = history.ToDictionary(v => v.Id, v => v.PublishedAt ?? DateTime.MinValue);
        var latest = acceptances
            .Where(a => a.DocumentKey == doc.Key && publishedAt.ContainsKey(a.DocumentVersionId))
            .OrderByDescending(a => publishedAt[a.DocumentVersionId])
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        var status = latest is null
            ? LegalAcceptanceStatuses.NotVerified
            : publishedAt[latest.DocumentVersionId] >= (baseline.PublishedAt ?? DateTime.MinValue)
                ? LegalAcceptanceStatuses.Accepted
                : LegalAcceptanceStatuses.Outdated;
        return new LegalRequirementDto(
            doc.Key, current.Title, doc.RequiredAction, status, current.Version, latest?.Version, latest?.CreatedAt);
    }

    /// <summary>Versiones publicadas o retiradas por documento, de la más reciente a la más antigua.</summary>
    private async Task<IReadOnlyDictionary<string, List<LegalDocumentVersion>>> PublishedHistoryAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        var rows = await _db.Set<LegalDocumentVersion>().AsNoTracking()
            .Where(v => keys.Contains(v.DocumentKey) && v.Status != LegalVersionStatuses.Draft)
            .ToListAsync(ct);
        return rows
            .GroupBy(v => v.DocumentKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.PublishedAt).ThenByDescending(v => v.Id).ToList());
    }

    private async Task<Dictionary<string, LegalDocumentVersion>> CurrentVersionsAsync(CancellationToken ct)
    {
        var rows = await _db.Set<LegalDocumentVersion>().AsNoTracking()
            .Where(v => v.Status == LegalVersionStatuses.Published)
            .ToListAsync(ct);
        return rows
            .GroupBy(v => v.DocumentKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.PublishedAt).First());
    }

    private async Task<LegalDocumentVersion> FindVersionAsync(int id, CancellationToken ct) =>
        await _db.Set<LegalDocumentVersion>().FirstOrDefaultAsync(v => v.Id == id, ct)
        ?? throw new NotFoundException("Versión no encontrada.", "legal_version_not_found");

    private async Task EnsureVersionFreeAsync(string key, string version, int? exceptId, CancellationToken ct)
    {
        var taken = await _db.Set<LegalDocumentVersion>()
            .AnyAsync(v => v.DocumentKey == key && v.Version == version && v.Id != exceptId, ct);
        if (taken)
        {
            throw new ConflictException("Ya existe esa versión para este documento.", "legal_version_exists");
        }
    }

    private async Task<IReadOnlyList<LegalAcceptanceDto>> AcceptanceQuery(
        IQueryable<UserLegalAcceptance> query,
        int take,
        CancellationToken ct)
    {
        var rows = await query.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(take)
            .ToListAsync(ct);
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await _db.Set<User>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(u => u.Id, ct);
        return rows.Select(r =>
        {
            users.TryGetValue(r.UserId, out var user);
            return new LegalAcceptanceDto(
                r.Id, r.UserId, user?.Name, user?.Email, r.DocumentKey, r.Version,
                r.Action, r.Method, r.IpAddress, r.UserAgent, r.CreatedAt);
        }).ToList();
    }

    private static LegalVersionDto ToDto(LegalDocumentVersion v, int acceptances) =>
        new(v.Id, v.DocumentKey, v.Version, v.Title, v.Content, v.ContentHash, v.Status,
            v.RequiresReacceptance, v.HasPlaceholders, v.ChangeSummary, v.CreatedAt, v.PublishedAt, acceptances);

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        // Evita inyección de fórmulas al abrir el CSV en Excel.
        var safe = value.Length > 0 && "=+-@".Contains(value[0]) ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
