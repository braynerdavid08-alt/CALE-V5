using System.Text.Json;
using System.Text.RegularExpressions;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Catalog;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.Commands;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Engagement.Domain;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cale.Api.Services.Requests;

public sealed record QuestionDraftOption(string? Text, bool IsCorrect, string? ImageUrl);

public sealed record QuestionDraft(
    string Text,
    string Type,
    string? Topic,
    string? ImageUrl,
    string? Explanation,
    IReadOnlyList<QuestionDraftOption> Options);

public sealed record CreateUserRequest(
    string Kind,
    string? Title,
    string? Message,
    QuestionDraft? Question,
    int? QuestionId = null);

/// <summary>Payload of a <see cref="UserRequestKinds.Report"/> request.</summary>
public sealed record QuestionReportPayload(int QuestionId);

public sealed record SimilarQuestionsRequest(string? Text, int? ExcludeQuestionId = null);

public sealed record SimilarQuestionDto(int QuestionId, string Text, string BankName, int Percent);

public sealed record AcceptUserRequest(
    string? Note,
    int? BankId,
    int? BlockId,
    QuestionDraft? Question);

public sealed record RejectUserRequest(string? Note);

public sealed record UserRequestDto(
    int Id,
    int UserId,
    string UserName,
    string UserRole,
    string Kind,
    string Status,
    string? Title,
    string? Message,
    QuestionDraft? Question,
    string? AdminNote,
    DateTime? ReviewedAt,
    int? CreatedQuestionId,
    DateTime CreatedAt,
    int? ReportedQuestionId = null);

public sealed record UserRequestCountsDto(int Pending, int Accepted, int Rejected);

public sealed record BlockUserRequest(string? Reason);

public sealed record BlockedUserDto(int UserId, string Name, string Email, string? Reason, DateTime CreatedAt);

public sealed record MyRequestStatusDto(bool Blocked, string? Reason);

/// <summary>
/// Proposals sent by users to the admin (CALE question drafts and ideas).
/// The admin decides; accepting a question creates it in the chosen bank.
/// </summary>
public sealed class UserRequestService
{
    private const int MaxPendingPerUser = 10;
    private const int MaxPerDay = 15;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly INotificationPublisher _notifications;
    private readonly SaveQuestionHandler _saveQuestion;
    private readonly IMemoryCache _cache;
    private readonly ICatalogMediaStore _media;

    public UserRequestService(
        CaleDbContext db,
        IClock clock,
        INotificationPublisher notifications,
        SaveQuestionHandler saveQuestion,
        IMemoryCache cache,
        ICatalogMediaStore media)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
        _saveQuestion = saveQuestion;
        _cache = cache;
        _media = media;
    }

    public async Task<UserRequestDto> CreateAsync(int userId, CreateUserRequest request, CancellationToken ct)
    {
        var user = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");
        var kind = (request.Kind ?? string.Empty).Trim().ToLowerInvariant();
        if (!UserRequestKinds.IsValid(kind))
        {
            throw new DomainException("Tipo de solicitud inválido.", 400, "invalid_request_kind");
        }

        if (await _db.Set<UserRequestBlock>().AnyAsync(b => b.UserId == userId, ct))
        {
            throw new DomainException(
                "El administrador desactivó el envío de solicitudes para tu cuenta.",
                403,
                "requests_blocked");
        }

        var now = _clock.UtcNow;
        var pending = await _db.Set<UserRequest>()
            .CountAsync(r => r.UserId == userId && r.Status == UserRequestStatuses.Pending, ct);
        if (pending >= MaxPendingPerUser)
        {
            throw new DomainException(
                $"Tienes {pending} solicitudes pendientes. Espera la respuesta del administrador antes de enviar más.",
                400,
                "too_many_pending_requests");
        }

        var since = now.AddDays(-1);
        var today = await _db.Set<UserRequest>()
            .CountAsync(r => r.UserId == userId && r.CreatedAt >= since, ct);
        if (today >= MaxPerDay)
        {
            throw new DomainException(
                "Alcanzaste el máximo de solicitudes por hoy. Intenta mañana.",
                400,
                "too_many_requests_today");
        }

        string? title = Clip(request.Title, 200);
        string? message = Clip(request.Message, 4000);
        string? payload = null;
        if (kind == UserRequestKinds.Question)
        {
            var draft = Normalize(request.Question
                ?? throw new DomainException("Completa la pregunta.", 400, "invalid_question"));
            payload = JsonSerializer.Serialize(draft, Json);
            title ??= Clip(draft.Text, 200);
        }
        else if (kind == UserRequestKinds.Report)
        {
            var questionId = request.QuestionId
                ?? throw new DomainException("Falta la pregunta a reportar.", 400, "invalid_report");
            var text = await _db.Set<Question>().AsNoTracking()
                .Where(q => q.Id == questionId)
                .Select(q => q.Text)
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Pregunta no encontrada.", "question_not_found");
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new DomainException("Cuéntanos qué está mal en la pregunta.", 400, "invalid_report");
            }

            payload = JsonSerializer.Serialize(new QuestionReportPayload(questionId), Json);
            var marker = $"\"questionId\":{questionId}}}";
            var already = await _db.Set<UserRequest>().AnyAsync(
                r => r.UserId == userId
                    && r.Kind == UserRequestKinds.Report
                    && r.Status == UserRequestStatuses.Pending
                    && r.PayloadJson != null
                    && r.PayloadJson.Contains(marker),
                ct);
            if (already)
            {
                throw new DomainException("Ya reportaste esta pregunta. El administrador la está revisando.", 400, "already_reported");
            }

            title = Clip(text, 200);
        }
        else if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            throw new DomainException("Escribe un título y describe tu idea.", 400, "invalid_idea");
        }

        var entity = UserRequest.Create(
            userId,
            user.Name,
            Roles.Normalize(user.Role),
            kind,
            title,
            message,
            payload,
            now);
        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        await NotifyAdminsAsync(entity, ct);
        return Map(entity);
    }

    public async Task<IReadOnlyList<UserRequestDto>> ListMineAsync(int userId, CancellationToken ct)
    {
        var rows = await _db.Set<UserRequest>().AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task CancelAsync(int userId, int id, CancellationToken ct)
    {
        var entity = await _db.Set<UserRequest>().FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct)
            ?? throw new NotFoundException("Solicitud no encontrada.", "request_not_found");
        if (!entity.IsPending)
        {
            throw new DomainException("Solo puedes retirar solicitudes pendientes.", 400, "request_not_pending");
        }

        var draft = ParseDraft(entity);
        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await DeleteUnusedImagesAsync(userId, ImageUrls(draft), ct);
    }

    public async Task<IReadOnlyList<UserRequestDto>> ListForAdminAsync(string? status, string? kind, CancellationToken ct)
    {
        var query = _db.Set<UserRequest>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            query = query.Where(r => r.Kind == kind);
        }

        var rows = await query.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    /// <summary>
    /// Active questions whose wording overlaps the given text (word-set similarity), to spot
    /// duplicates before accepting a proposal.
    /// </summary>
    public async Task<IReadOnlyList<SimilarQuestionDto>> FindSimilarAsync(SimilarQuestionsRequest request, CancellationToken ct)
    {
        var target = Words(request.Text);
        if (target.Count < 2)
        {
            return [];
        }

        var rows = await _db.Set<Question>().AsNoTracking()
            .Where(q => q.IsActive && (request.ExcludeQuestionId == null || q.Id != request.ExcludeQuestionId))
            .Select(q => new { q.Id, q.Text, q.BankId })
            .ToListAsync(ct);

        var best = rows
            .Select(q =>
            {
                var words = Words(q.Text);
                var shared = words.Count(target.Contains);
                var union = words.Count + target.Count - shared;
                return new { q.Id, q.Text, q.BankId, Score = union == 0 ? 0 : (double)shared / union };
            })
            .Where(x => x.Score >= 0.45)
            .OrderByDescending(x => x.Score)
            .Take(5)
            .ToList();
        if (best.Count == 0)
        {
            return [];
        }

        var bankIds = best.Select(x => x.BankId).Distinct().ToList();
        var banks = await _db.Set<Bank>().AsNoTracking()
            .Where(b => bankIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        return best
            .Select(x => new SimilarQuestionDto(
                x.Id,
                x.Text,
                banks.GetValueOrDefault(x.BankId, ""),
                (int)Math.Round(x.Score * 100)))
            .ToList();
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "que", "los", "las", "del", "por", "para", "con", "una", "uno", "unos", "unas", "como", "cual",
        "cuál", "debe", "esta", "este", "esto", "son", "sus", "más", "mas", "sin", "sobre", "entre",
        "cuando", "donde", "segun", "según", "puede", "pueden", "tiene", "hay", "ser", "está", "estan"
    };

    private static HashSet<string> Words(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = new string(text.ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray());
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<MyRequestStatusDto> MyStatusAsync(int userId, CancellationToken ct)
    {
        var block = await _db.Set<UserRequestBlock>().AsNoTracking().FirstOrDefaultAsync(b => b.UserId == userId, ct);
        return new MyRequestStatusDto(block is not null, block?.Reason);
    }

    public async Task<IReadOnlyList<BlockedUserDto>> ListBlockedAsync(CancellationToken ct)
    {
        var blocks = await _db.Set<UserRequestBlock>().AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
        var ids = blocks.Select(b => b.UserId).ToList();
        var users = await _db.Set<User>().AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(u => u.Id, ct);
        return blocks
            .Select(b => users.TryGetValue(b.UserId, out var u)
                ? new BlockedUserDto(b.UserId, u.Name, u.Email, b.Reason, b.CreatedAt)
                : new BlockedUserDto(b.UserId, $"Usuario #{b.UserId}", string.Empty, b.Reason, b.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Stops a user from sending requests and rejects everything they still have pending.
    /// Returns how many pending requests were rejected.
    /// </summary>
    public async Task<int> BlockAsync(int adminId, int userId, BlockUserRequest request, CancellationToken ct)
    {
        var role = await _db.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");
        if (Roles.Normalize(role) == Roles.Admin)
        {
            throw new DomainException("No puedes bloquear a un administrador.", 400, "cannot_block_admin");
        }

        var reason = Clip(request.Reason, 500);
        if (!await _db.Set<UserRequestBlock>().AnyAsync(b => b.UserId == userId, ct))
        {
            _db.Add(UserRequestBlock.Create(userId, adminId, reason, _clock.UtcNow));
        }

        var pending = await _db.Set<UserRequest>()
            .Where(r => r.UserId == userId && r.Status == UserRequestStatuses.Pending)
            .ToListAsync(ct);
        var images = new List<string>();
        foreach (var entity in pending)
        {
            var draft = ParseDraft(entity);
            images.AddRange(ImageUrls(draft));
            entity.Reject(adminId, "Envío de solicitudes desactivado.", _clock.UtcNow, StripImages(draft));
        }

        await _db.SaveChangesAsync(ct);
        await DeleteUnusedImagesAsync(userId, images, ct);

        try
        {
            await _notifications.NotifyUsersAsync(
                [userId],
                new NotificationDraft(
                    "Solicitudes desactivadas",
                    Append("El administrador desactivó el envío de preguntas e ideas para tu cuenta.", reason),
                    NotificationTypes.System,
                    Link: "/solicitudes"),
                ct);
        }
        catch
        {
            // Best-effort.
        }

        return pending.Count;
    }

    public async Task UnblockAsync(int userId, CancellationToken ct)
    {
        await _db.Set<UserRequestBlock>().Where(b => b.UserId == userId).ExecuteDeleteAsync(ct);
    }

    public async Task<UserRequestCountsDto> CountsAsync(CancellationToken ct)
    {
        var groups = await _db.Set<UserRequest>().AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Get(string s) => groups.FirstOrDefault(g => g.Key == s)?.Count ?? 0;
        return new UserRequestCountsDto(
            Get(UserRequestStatuses.Pending),
            Get(UserRequestStatuses.Accepted),
            Get(UserRequestStatuses.Rejected));
    }

    public async Task<UserRequestDto> AcceptAsync(int adminId, int id, AcceptUserRequest request, CancellationToken ct)
    {
        var entity = await RequirePendingAsync(id, ct);
        var note = Clip(request.Note, 1000);
        int? questionId = null;
        string? finalPayload = null;
        List<string> droppedImages = [];

        if (entity.Kind == UserRequestKinds.Question)
        {
            if (request.BankId is not int bankId || request.BlockId is not int blockId)
            {
                throw new DomainException("Elige el banco y el bloque donde irá la pregunta.", 400, "bank_required");
            }

            var original = ParseDraft(entity);
            var draft = Normalize(request.Question ?? original
                ?? throw new DomainException("La solicitud no tiene pregunta.", 400, "invalid_question"));
            droppedImages = ImageUrls(original).Except(ImageUrls(draft)).ToList();
            questionId = await _saveQuestion.CreateAsync(
                new SaveQuestionRequest(
                    bankId,
                    blockId,
                    draft.Text,
                    draft.Type,
                    draft.Topic,
                    draft.ImageUrl,
                    draft.Explanation,
                    true,
                    draft.Options.Select(o => new OptionInput(o.Text ?? string.Empty, o.IsCorrect, o.ImageUrl)).ToList()),
                adminId,
                isAdmin: true,
                ct);
            finalPayload = JsonSerializer.Serialize(draft, Json);
            _cache.Remove("play:official-questions");
            _cache.Remove("play:signs-questions");
            _cache.Remove("play:official-blocks");
        }

        entity.Accept(adminId, note, questionId, finalPayload, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await DeleteUnusedImagesAsync(entity.UserId, droppedImages, ct);

        var what = entity.Kind switch
        {
            UserRequestKinds.Question => "Tu pregunta fue aceptada y ya forma parte de CALE. ¡Gracias por aportar!",
            UserRequestKinds.Report => $"Revisamos la pregunta que reportaste («{entity.Title}»). ¡Gracias por avisar!",
            _ => "Tu idea fue aceptada. ¡Gracias por ayudar a mejorar CALE!"
        };
        await NotifyRequesterAsync(entity, "Solicitud aceptada ✅", Append(what, note), ct);
        return Map(entity);
    }

    public async Task<UserRequestDto> RejectAsync(int adminId, int id, RejectUserRequest request, CancellationToken ct)
    {
        var entity = await RequirePendingAsync(id, ct);
        var note = Clip(request.Note, 1000);
        var draft = ParseDraft(entity);
        var images = ImageUrls(draft).ToList();
        entity.Reject(adminId, note, _clock.UtcNow, StripImages(draft));
        await _db.SaveChangesAsync(ct);
        await DeleteUnusedImagesAsync(entity.UserId, images, ct);
        await NotifyRequesterAsync(
            entity,
            "Solicitud revisada",
            Append(
                entity.Kind == UserRequestKinds.Report
                    ? $"Revisamos la pregunta que reportaste («{entity.Title}») y no encontramos un error."
                    : $"Tu solicitud «{entity.Title}» no fue aceptada esta vez.",
                note),
            ct);
        return Map(entity);
    }

    private async Task<UserRequest> RequirePendingAsync(int id, CancellationToken ct)
    {
        var entity = await _db.Set<UserRequest>().FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Solicitud no encontrada.", "request_not_found");
        if (!entity.IsPending)
        {
            throw new DomainException("Esta solicitud ya fue revisada.", 400, "request_not_pending");
        }

        return entity;
    }

    private async Task NotifyAdminsAsync(UserRequest entity, CancellationToken ct)
    {
        var adminIds = await _db.Set<User>().AsNoTracking()
            .Where(u => u.IsActive && u.Role == Roles.Admin)
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (adminIds.Count == 0)
        {
            return;
        }

        var what = entity.Kind switch
        {
            UserRequestKinds.Question => "propone una pregunta",
            UserRequestKinds.Report => "reportó una pregunta",
            _ => "envió una idea"
        };
        try
        {
            await _notifications.NotifyUsersAsync(
                adminIds,
                new NotificationDraft(
                    "Nueva solicitud de usuario",
                    $"{entity.UserName} {what}: «{entity.Title}».",
                    NotificationTypes.Admin,
                    RelatedEntity: "user_request",
                    RelatedId: entity.Id,
                    Link: "/admin/requests"),
                ct);
        }
        catch
        {
            // Notifying is best-effort; the request is already saved.
        }
    }

    private async Task NotifyRequesterAsync(UserRequest entity, string title, string message, CancellationToken ct)
    {
        try
        {
            await _notifications.NotifyUsersAsync(
                [entity.UserId],
                new NotificationDraft(
                    title,
                    message,
                    NotificationTypes.System,
                    RelatedEntity: "user_request",
                    RelatedId: entity.Id,
                    Link: "/solicitudes"),
                ct);
        }
        catch
        {
            // Best-effort.
        }
    }

    private static QuestionDraft Normalize(QuestionDraft draft)
    {
        var text = (draft.Text ?? string.Empty).Trim();
        if (text.Length < 5)
        {
            throw new DomainException("Escribe el enunciado de la pregunta.", 400, "invalid_text");
        }

        if (!QuestionTypes.IsValid(draft.Type))
        {
            throw new DomainException("Tipo de pregunta inválido.", 400, "invalid_type");
        }

        var options = (draft.Options ?? [])
            .Select(o => new QuestionDraftOption(Clip(o.Text, 500), o.IsCorrect, Clip(o.ImageUrl, 500)))
            .ToList();
        if (options.Count < 2 || options.Count > 6)
        {
            throw new DomainException("La pregunta debe tener entre 2 y 6 respuestas.", 400, "invalid_options");
        }

        if (options.Any(o => string.IsNullOrWhiteSpace(o.Text) && string.IsNullOrWhiteSpace(o.ImageUrl)))
        {
            throw new DomainException("Cada respuesta necesita texto o imagen.", 400, "invalid_options");
        }

        if (options.Count(o => o.IsCorrect) != 1)
        {
            throw new DomainException("Marca exactamente una respuesta correcta.", 400, "invalid_correct");
        }

        return new QuestionDraft(
            Clip(text, 2000)!,
            draft.Type,
            Clip(draft.Topic, 200),
            Clip(draft.ImageUrl, 500),
            Clip(draft.Explanation, 2000),
            options);
    }

    /// <summary>Question draft of a proposal; null for ideas and reports.</summary>
    private static QuestionDraft? ParseDraft(UserRequest r)
    {
        if (r.Kind != UserRequestKinds.Question || string.IsNullOrWhiteSpace(r.PayloadJson))
        {
            return null;
        }

        try
        {
            var draft = JsonSerializer.Deserialize<QuestionDraft>(r.PayloadJson, Json);
            return draft is { Options: not null } ? draft : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ReportedQuestionId(UserRequest r)
    {
        if (r.Kind != UserRequestKinds.Report || string.IsNullOrWhiteSpace(r.PayloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QuestionReportPayload>(r.PayloadJson, Json)?.QuestionId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static UserRequestDto Map(UserRequest r) => new(
        r.Id,
        r.UserId,
        r.UserName,
        r.UserRole,
        r.Kind,
        r.Status,
        r.Title,
        r.Message,
        ParseDraft(r),
        r.AdminNote,
        r.ReviewedAt,
        r.CreatedQuestionId,
        r.CreatedAt,
        ReportedQuestionId(r));

    private static readonly Regex MediaUrl = new(
        @"/api/media/(?<id>[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12})",
        RegexOptions.Compiled);

    /// <summary>Payload without image URLs, or null when there is nothing to strip.</summary>
    private static string? StripImages(QuestionDraft? draft) =>
        draft is not null && ImageUrls(draft).Any()
            ? JsonSerializer.Serialize(
                draft with
                {
                    ImageUrl = null,
                    Options = draft.Options.Select(o => o with { ImageUrl = null }).ToList()
                },
                Json)
            : null;

    private static IEnumerable<string> ImageUrls(QuestionDraft? draft)
    {
        if (draft is null)
        {
            return [];
        }

        return new[] { draft.ImageUrl }
            .Concat(draft.Options.Select(o => o.ImageUrl))
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!.Trim())
            .Distinct();
    }

    /// <summary>
    /// Deletes images the requester uploaded for a proposal once nothing references them,
    /// so rejected or withdrawn proposals do not keep growing the database.
    /// </summary>
    private async Task DeleteUnusedImagesAsync(int ownerId, IEnumerable<string> urls, CancellationToken ct)
    {
        var candidates = urls
            .Select(u => MediaUrl.Match(u))
            .Where(m => m.Success && Guid.TryParse(m.Groups["id"].Value, out _))
            .Select(m => Guid.Parse(m.Groups["id"].Value))
            .Distinct()
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var stillUsed = new HashSet<Guid>();
        foreach (var id in candidates)
        {
            var key = id.ToString("D");
            var keyN = id.ToString("N");
            var used = await _db.Set<Question>().AsNoTracking()
                    .AnyAsync(q => q.ImageUrl != null && (q.ImageUrl.Contains(key) || q.ImageUrl.Contains(keyN)), ct)
                || await _db.Set<QuestionOption>().AsNoTracking()
                    .AnyAsync(o => o.ImageUrl != null && (o.ImageUrl.Contains(key) || o.ImageUrl.Contains(keyN)), ct)
                || await _db.Set<UserRequest>().AsNoTracking()
                    .AnyAsync(r => r.PayloadJson != null && (r.PayloadJson.Contains(key) || r.PayloadJson.Contains(keyN)), ct);
            if (used)
            {
                stillUsed.Add(id);
            }
        }

        var ids = candidates.Where(id => !stillUsed.Contains(id)).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        await _media.DeleteOwnedAsync(ids, ownerId, ct);
    }

    private static string Append(string message, string? note) =>
        string.IsNullOrWhiteSpace(note) ? message : $"{message} Nota del administrador: {note}";

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
