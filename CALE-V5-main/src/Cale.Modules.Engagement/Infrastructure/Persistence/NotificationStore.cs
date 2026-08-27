using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Engagement.Application.Abstractions;
using Cale.Modules.Engagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Engagement.Infrastructure.Persistence;

public sealed class NotificationStore : INotificationStore
{
    private readonly CaleDbContext _db;

    public NotificationStore(CaleDbContext db) => _db = db;

    public async Task AddRangeAsync(
        IReadOnlyList<AppNotification> items,
        CancellationToken ct) =>
        await _db.Set<AppNotification>().AddRangeAsync(items, ct);

    public async Task<IReadOnlyList<AppNotification>> ListByUserAsync(
        NotificationListQuery query,
        CancellationToken ct)
    {
        var take = Math.Clamp(query.Take, 1, 100);
        var skip = Math.Max(0, query.Skip);

        var q = _db.Set<AppNotification>()
            .AsQueryable()
            .Where(x => x.UserId == query.UserId && !x.IsArchived);

        if (query.UnreadOnly == true)
        {
            q = q.Where(x => !x.IsRead);
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            q = q.Where(x => x.Type == query.Type);
        }
        else if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var types = TypesForCategory(query.Category!);
            q = q.Where(x => types.Contains(x.Type));
        }

        return await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<AppNotification?> GetAsync(int id, CancellationToken ct) =>
        _db.Set<AppNotification>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct) =>
        _db.Set<AppNotification>().CountAsync(
            x => x.UserId == userId && !x.IsRead && !x.IsArchived,
            ct);

    public async Task<int> MarkAllReadAsync(
        int userId,
        DateTime utcNow,
        CancellationToken ct)
    {
        var items = await _db.Set<AppNotification>()
            .Where(x => x.UserId == userId && !x.IsRead && !x.IsArchived)
            .ToListAsync(ct);
        foreach (var item in items)
        {
            item.MarkRead(utcNow);
        }

        return items.Count;
    }

    public async Task<HashSet<int>> FindUsersWithDedupeAsync(
        IReadOnlyList<int> userIds,
        string dedupeKey,
        CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var found = await _db.Set<AppNotification>()
            .Where(x =>
                x.DedupeKey == dedupeKey
                && userIds.Contains(x.UserId)
                && !x.IsArchived)
            .Select(x => x.UserId)
            .ToListAsync(ct);
        return found.ToHashSet();
    }

    public Task<NotificationPreference?> GetPreferenceAsync(
        int userId,
        CancellationToken ct) =>
        _db.Set<NotificationPreference>()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task AddPreferenceAsync(
        NotificationPreference preference,
        CancellationToken ct) =>
        await _db.Set<NotificationPreference>().AddAsync(preference, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);

    private static string[] TypesForCategory(string category) => category switch
    {
        NotificationCategories.Academic =>
        [
            NotificationTypes.Announcement,
            NotificationTypes.Material,
            NotificationTypes.Activity,
            NotificationTypes.Exam,
            NotificationTypes.ExamResult,
            NotificationTypes.Grade,
            NotificationTypes.Submission
        ],
        NotificationCategories.Membership => [NotificationTypes.Membership],
        NotificationCategories.Admin => [NotificationTypes.Admin],
        NotificationCategories.System => [NotificationTypes.System],
        _ => []
    };
}
