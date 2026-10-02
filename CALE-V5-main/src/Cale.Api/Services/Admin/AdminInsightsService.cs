using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Engagement.Domain;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Admin;

public sealed record RoleCountDto(string Role, int Count);

public sealed record WeeklySummaryDto(
    DateTime From,
    DateTime To,
    int NewUsers,
    IReadOnlyList<RoleCountDto> NewUsersByRole,
    int ActiveStudents,
    int ExamsFinished,
    int GamesPlayed,
    int NewRequests,
    int PendingRequests,
    int PendingReports);

public sealed record StorageOwnerDto(string Group, int Files, long Bytes);

public sealed record StorageFileDto(Guid Id, string ContentType, long Bytes, string Owner, DateTime CreatedAt);

public sealed record StorageReportDto(
    int Files,
    long Bytes,
    long? DatabaseBytes,
    IReadOnlyList<StorageOwnerDto> ByOwner,
    IReadOnlyList<StorageFileDto> Largest);

public sealed record QuestionAccuracyDto(int QuestionId, string Text, string BankName, int Answers, int Percent);

/// <summary>Usage numbers for the admin: weekly activity, image storage and hardest questions.</summary>
public sealed class AdminInsightsService
{
    public const int MinAnswersForAccuracy = 8;

    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly INotificationPublisher _notifications;

    public AdminInsightsService(CaleDbContext db, IClock clock, INotificationPublisher notifications)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<WeeklySummaryDto> GetWeeklySummaryAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 90);
        var to = _clock.UtcNow;
        var from = to.AddDays(-days);

        var newUsers = await _db.Set<User>().AsNoTracking()
            .Where(u => u.CreatedAt >= from)
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byRole = newUsers
            .GroupBy(x => Roles.Normalize(x.Role))
            .Select(g => new RoleCountDto(g.Key, g.Sum(x => x.Count)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var examUsers = await _db.Set<Attempt>().AsNoTracking()
            .Where(a => a.FinishedAt != null && a.FinishedAt >= from)
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(ct);
        var gameUsers = await _db.Set<GameResult>().AsNoTracking()
            .Where(g => g.PlayedAt >= from)
            .Select(g => g.UserId)
            .Distinct()
            .ToListAsync(ct);
        var examsFinished = await _db.Set<Attempt>().AsNoTracking()
            .CountAsync(a => a.FinishedAt != null && a.FinishedAt >= from, ct);
        var gamesPlayed = await _db.Set<GameResult>().AsNoTracking()
            .CountAsync(g => g.PlayedAt >= from, ct);

        var newRequests = await _db.Set<UserRequest>().AsNoTracking().CountAsync(r => r.CreatedAt >= from, ct);
        var pending = await _db.Set<UserRequest>().AsNoTracking()
            .Where(r => r.Status == UserRequestStatuses.Pending)
            .GroupBy(r => r.Kind)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new WeeklySummaryDto(
            from,
            to,
            byRole.Sum(x => x.Count),
            byRole,
            examUsers.Union(gameUsers).Count(),
            examsFinished,
            gamesPlayed,
            newRequests,
            pending.Sum(x => x.Count),
            pending.Where(x => x.Key == UserRequestKinds.Report).Sum(x => x.Count));
    }

    public async Task<StorageReportDto> GetStorageAsync(CancellationToken ct)
    {
        var blobs = await _db.Set<CatalogMediaBlob>().AsNoTracking()
            .Select(b => new { b.Id, b.ContentType, b.OwnerId, b.CreatedAt, Bytes = (long)b.Data.Length })
            .ToListAsync(ct);

        var ownerIds = blobs.Where(b => b.OwnerId != null).Select(b => b.OwnerId!.Value).Distinct().ToList();
        var owners = await _db.Set<User>().AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Role })
            .ToDictionaryAsync(u => u.Id, ct);

        string GroupOf(int? ownerId) =>
            ownerId is int id && owners.TryGetValue(id, out var u)
                ? Roles.Normalize(u.Role) switch
                {
                    Roles.Admin => "Administrador",
                    Roles.Teacher => "Docentes",
                    Roles.School => "Escuelas",
                    _ => "Estudiantes"
                }
                : "Sistema / migradas";

        var byOwner = blobs
            .GroupBy(b => GroupOf(b.OwnerId))
            .Select(g => new StorageOwnerDto(g.Key, g.Count(), g.Sum(x => x.Bytes)))
            .OrderByDescending(x => x.Bytes)
            .ToList();
        var largest = blobs
            .OrderByDescending(b => b.Bytes)
            .Take(10)
            .Select(b => new StorageFileDto(
                b.Id,
                b.ContentType,
                b.Bytes,
                b.OwnerId is int id && owners.TryGetValue(id, out var u) ? u.Name : "Sistema",
                b.CreatedAt))
            .ToList();

        return new StorageReportDto(
            blobs.Count,
            blobs.Sum(b => b.Bytes),
            await DatabaseSizeAsync(ct),
            byOwner,
            largest);
    }

    private async Task<long?> DatabaseSizeAsync(CancellationToken ct)
    {
        if (!_db.Database.IsNpgsql())
        {
            return null;
        }

        try
        {
            return await _db.Database
                .SqlQueryRaw<long>("SELECT pg_database_size(current_database()) AS \"Value\"")
                .FirstAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Active questions with the lowest share of correct answers in finished exams (at least
    /// <see cref="MinAnswersForAccuracy"/> answers). Very low numbers often mean a wrong answer key.
    /// </summary>
    public async Task<IReadOnlyList<QuestionAccuracyDto>> GetHardestQuestionsAsync(int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 5, 100);
        var stats = await _db.Set<AttemptAnswer>().AsNoTracking()
            .GroupBy(a => a.QuestionId)
            .Select(g => new { QuestionId = g.Key, Total = g.Count(), Correct = g.Count(a => a.IsCorrect) })
            .Where(x => x.Total >= MinAnswersForAccuracy)
            .ToListAsync(ct);
        if (stats.Count == 0)
        {
            return [];
        }

        var ordered = stats
            .OrderBy(x => (double)x.Correct / x.Total)
            .ThenByDescending(x => x.Total)
            .Take(take * 2)
            .ToList();
        var ids = ordered.Select(x => x.QuestionId).ToList();
        var questions = await _db.Set<Question>().AsNoTracking()
            .Where(q => ids.Contains(q.Id) && q.IsActive)
            .Select(q => new { q.Id, q.Text, q.BankId })
            .ToDictionaryAsync(q => q.Id, ct);
        var bankIds = questions.Values.Select(q => q.BankId).Distinct().ToList();
        var banks = await _db.Set<Bank>().AsNoTracking()
            .Where(b => bankIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        return ordered
            .Where(x => questions.ContainsKey(x.QuestionId))
            .Take(take)
            .Select(x =>
            {
                var q = questions[x.QuestionId];
                return new QuestionAccuracyDto(
                    q.Id,
                    q.Text,
                    banks.GetValueOrDefault(q.BankId, ""),
                    x.Total,
                    (int)Math.Round(100.0 * x.Correct / x.Total));
            })
            .ToList();
    }

    /// <summary>Sends the weekly summary to every active admin; the dedupe key keeps it to one per week.</summary>
    public async Task<int> SendWeeklySummaryAsync(string weekKey, CancellationToken ct)
    {
        var adminIds = await _db.Set<User>().AsNoTracking()
            .Where(u => u.IsActive && u.Role == Roles.Admin)
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (adminIds.Count == 0)
        {
            return 0;
        }

        var s = await GetWeeklySummaryAsync(7, ct);
        var message =
            $"Última semana: {s.NewUsers} usuarios nuevos, {s.ActiveStudents} activos, " +
            $"{s.ExamsFinished} exámenes y {s.GamesPlayed} partidas. " +
            $"Solicitudes pendientes: {s.PendingRequests}" +
            (s.PendingReports > 0 ? $" ({s.PendingReports} reportes de preguntas)." : ".");
        await _notifications.NotifyUsersAsync(
            adminIds,
            new NotificationDraft(
                "Resumen semanal de Luz Verde",
                message,
                NotificationTypes.Admin,
                Link: "/admin/usage",
                DedupeKey: $"weekly-summary:{weekKey}"),
            ct);
        return adminIds.Count;
    }
}
