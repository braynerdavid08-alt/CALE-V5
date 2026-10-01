using System.Text.Json;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Catalog;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Application.Commands;
using Cale.Modules.Catalog.Application.DTOs;
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
    QuestionDraft? Question);

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
    DateTime CreatedAt);

public sealed record UserRequestCountsDto(int Pending, int Accepted, int Rejected);

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

    public UserRequestService(
        CaleDbContext db,
        IClock clock,
        INotificationPublisher notifications,
        SaveQuestionHandler saveQuestion,
        IMemoryCache cache)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
        _saveQuestion = saveQuestion;
        _cache = cache;
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

        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);
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

        if (entity.Kind == UserRequestKinds.Question)
        {
            if (request.BankId is not int bankId || request.BlockId is not int blockId)
            {
                throw new DomainException("Elige el banco y el bloque donde irá la pregunta.", 400, "bank_required");
            }

            var draft = Normalize(request.Question ?? ParseDraft(entity.PayloadJson)
                ?? throw new DomainException("La solicitud no tiene pregunta.", 400, "invalid_question"));
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
            _cache.Remove("play:official-blocks");
        }

        entity.Accept(adminId, note, questionId, finalPayload, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        var what = entity.Kind == UserRequestKinds.Question
            ? "Tu pregunta fue aceptada y ya forma parte de CALE. ¡Gracias por aportar!"
            : "Tu idea fue aceptada. ¡Gracias por ayudar a mejorar CALE!";
        await NotifyRequesterAsync(entity, "Solicitud aceptada ✅", Append(what, note), ct);
        return Map(entity);
    }

    public async Task<UserRequestDto> RejectAsync(int adminId, int id, RejectUserRequest request, CancellationToken ct)
    {
        var entity = await RequirePendingAsync(id, ct);
        var note = Clip(request.Note, 1000);
        entity.Reject(adminId, note, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await NotifyRequesterAsync(
            entity,
            "Solicitud revisada",
            Append($"Tu solicitud «{entity.Title}» no fue aceptada esta vez.", note),
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

        var what = entity.Kind == UserRequestKinds.Question ? "propone una pregunta" : "envió una idea";
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

    private static QuestionDraft? ParseDraft(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QuestionDraft>(json, Json);
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
        ParseDraft(r.PayloadJson),
        r.AdminNote,
        r.ReviewedAt,
        r.CreatedQuestionId,
        r.CreatedAt);

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
