using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Assistant;

public interface IAssistantUsageStore
{
    Task<AssistantUsageSnapshot> GetSnapshotAsync(
        int userId,
        int? schoolUserId,
        int messagesLimit,
        CancellationToken ct);

    /// <summary>Atomically reserves one message if under limit. Returns false if at quota.</summary>
    Task<bool> TryReserveMessageAsync(
        int userId,
        int? schoolUserId,
        int messagesLimit,
        CancellationToken ct);

    Task CommitMessageAsync(int userId, int? schoolUserId, CancellationToken ct);

    Task ReleaseMessageAsync(int userId, int? schoolUserId, string reason, CancellationToken ct);

    Task<bool> TryReserveProviderRequestAsync(
        int userId,
        int? schoolUserId,
        int userRequestLimit,
        int schoolRequestLimit,
        int globalRequestLimit,
        CancellationToken ct);

    Task<AssistantSchoolUsageSummary> GetSchoolSummaryAsync(
        int schoolUserId,
        int messagesLimit,
        CancellationToken ct);

    Task<IReadOnlyList<AssistantSchoolUsageSummary>> ListSchoolsTodayAsync(
        int messagesLimit,
        CancellationToken ct);
}

public sealed class AssistantUsageStore : IAssistantUsageStore
{
    private readonly CaleDbContext _db;

    public AssistantUsageStore(CaleDbContext db) => _db = db;

    private static DateOnly Today() => ColombiaTime.TodayInColombia();

    public async Task<AssistantUsageSnapshot> GetSnapshotAsync(
        int userId,
        int? schoolUserId,
        int messagesLimit,
        CancellationToken ct)
    {
        var day = Today();
        var row = await _db.Set<AssistantUsageDaily>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day, ct);
        var used = row?.MessagesUsed ?? 0;
        var remaining = Math.Max(0, messagesLimit - used);
        return new AssistantUsageSnapshot(
            userId,
            schoolUserId ?? row?.SchoolUserId,
            day,
            used,
            messagesLimit,
            remaining,
            row?.RequestsUsed ?? 0,
            NearLimit: messagesLimit > 0 && used >= (int)Math.Ceiling(messagesLimit * 0.8),
            AtLimit: remaining <= 0);
    }

    public async Task<bool> TryReserveMessageAsync(
        int userId,
        int? schoolUserId,
        int messagesLimit,
        CancellationToken ct)
    {
        var day = Today();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var row = await _db.Set<AssistantUsageDaily>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day, ct);
        if (row is null)
        {
            row = new AssistantUsageDaily
            {
                UserId = userId,
                SchoolUserId = schoolUserId,
                Day = day,
                MessagesUsed = 0,
                RequestsUsed = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Set<AssistantUsageDaily>().Add(row);
            await _db.SaveChangesAsync(ct);
        }

        if (row.MessagesUsed >= messagesLimit)
        {
            _db.Set<AssistantUsageEvent>().Add(new AssistantUsageEvent
            {
                UserId = userId,
                SchoolUserId = schoolUserId ?? row.SchoolUserId,
                Day = day,
                Status = AssistantUsageStatuses.Rejected,
                Kind = AssistantUsageKinds.Message,
                Note = "quota_reached",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return false;
        }

        row.MessagesUsed += 1;
        if (schoolUserId is not null)
        {
            row.SchoolUserId = schoolUserId;
        }

        row.UpdatedAt = DateTime.UtcNow;
        _db.Set<AssistantUsageEvent>().Add(new AssistantUsageEvent
        {
            UserId = userId,
            SchoolUserId = row.SchoolUserId,
            Day = day,
            Status = AssistantUsageStatuses.Reserved,
            Kind = AssistantUsageKinds.Message,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task CommitMessageAsync(int userId, int? schoolUserId, CancellationToken ct)
    {
        _db.Set<AssistantUsageEvent>().Add(new AssistantUsageEvent
        {
            UserId = userId,
            SchoolUserId = schoolUserId,
            Day = Today(),
            Status = AssistantUsageStatuses.Committed,
            Kind = AssistantUsageKinds.Message,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReleaseMessageAsync(int userId, int? schoolUserId, string reason, CancellationToken ct)
    {
        var day = Today();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var row = await _db.Set<AssistantUsageDaily>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day, ct);
        if (row is not null && row.MessagesUsed > 0)
        {
            row.MessagesUsed -= 1;
            row.UpdatedAt = DateTime.UtcNow;
        }

        _db.Set<AssistantUsageEvent>().Add(new AssistantUsageEvent
        {
            UserId = userId,
            SchoolUserId = schoolUserId ?? row?.SchoolUserId,
            Day = day,
            Status = AssistantUsageStatuses.Released,
            Kind = AssistantUsageKinds.Message,
            Note = reason,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<bool> TryReserveProviderRequestAsync(
        int userId,
        int? schoolUserId,
        int userRequestLimit,
        int schoolRequestLimit,
        int globalRequestLimit,
        CancellationToken ct)
    {
        var day = Today();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var userRow = await _db.Set<AssistantUsageDaily>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day, ct);
        if (userRow is null)
        {
            userRow = new AssistantUsageDaily
            {
                UserId = userId,
                SchoolUserId = schoolUserId,
                Day = day,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Set<AssistantUsageDaily>().Add(userRow);
            await _db.SaveChangesAsync(ct);
        }

        if (userRow.RequestsUsed >= userRequestLimit)
        {
            await tx.CommitAsync(ct);
            return false;
        }

        if (schoolUserId is int schoolId)
        {
            var schoolUsed = await _db.Set<AssistantUsageDaily>()
                .Where(x => x.SchoolUserId == schoolId && x.Day == day)
                .SumAsync(x => (int?)x.RequestsUsed, ct) ?? 0;
            if (schoolUsed >= schoolRequestLimit)
            {
                await tx.CommitAsync(ct);
                return false;
            }
        }

        var globalUsed = await _db.Set<AssistantUsageDaily>()
            .Where(x => x.Day == day)
            .SumAsync(x => (int?)x.RequestsUsed, ct) ?? 0;
        if (globalUsed >= globalRequestLimit)
        {
            await tx.CommitAsync(ct);
            return false;
        }

        userRow.RequestsUsed += 1;
        userRow.UpdatedAt = DateTime.UtcNow;
        if (schoolUserId is not null)
        {
            userRow.SchoolUserId = schoolUserId;
        }

        _db.Set<AssistantUsageEvent>().Add(new AssistantUsageEvent
        {
            UserId = userId,
            SchoolUserId = userRow.SchoolUserId,
            Day = day,
            Status = AssistantUsageStatuses.Reserved,
            Kind = AssistantUsageKinds.ProviderRequest,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<AssistantSchoolUsageSummary> GetSchoolSummaryAsync(
        int schoolUserId,
        int messagesLimit,
        CancellationToken ct)
    {
        var day = Today();
        var rows = await _db.Set<AssistantUsageDaily>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId && x.Day == day)
            .ToListAsync(ct);

        var userIds = rows.Select(x => x.UserId).Distinct().ToList();
        var users = await _db.Set<Cale.Modules.Identity.Domain.User>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Role })
            .ToListAsync(ct);
        var byId = users.ToDictionary(u => u.Id);

        var studentRows = rows.Select(r =>
        {
            byId.TryGetValue(r.UserId, out var u);
            return new AssistantSchoolUsageRow(
                r.UserId,
                u?.Name ?? $"Usuario {r.UserId}",
                u?.Role ?? "",
                r.MessagesUsed,
                messagesLimit,
                Math.Max(0, messagesLimit - r.MessagesUsed));
        }).OrderByDescending(x => x.MessagesUsed).ToList();

        return new AssistantSchoolUsageSummary(
            day,
            schoolUserId,
            studentRows.Sum(x => x.MessagesUsed),
            studentRows.Count,
            studentRows);
    }

    public async Task<IReadOnlyList<AssistantSchoolUsageSummary>> ListSchoolsTodayAsync(
        int messagesLimit,
        CancellationToken ct)
    {
        var day = Today();
        var schoolIds = await _db.Set<AssistantUsageDaily>()
            .AsNoTracking()
            .Where(x => x.Day == day && x.SchoolUserId != null)
            .Select(x => x.SchoolUserId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var list = new List<AssistantSchoolUsageSummary>();
        foreach (var id in schoolIds)
        {
            list.Add(await GetSchoolSummaryAsync(id, messagesLimit, ct));
        }

        return list.OrderByDescending(x => x.TotalMessages).ToList();
    }
}
