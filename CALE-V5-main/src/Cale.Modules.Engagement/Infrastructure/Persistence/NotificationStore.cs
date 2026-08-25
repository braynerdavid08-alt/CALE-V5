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
        int userId,
        CancellationToken ct) =>
        await _db.Set<AppNotification>()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

    public Task<AppNotification?> GetAsync(int id, CancellationToken ct) =>
        _db.Set<AppNotification>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct) =>
        _db.Set<AppNotification>().CountAsync(
            x => x.UserId == userId && !x.IsRead,
            ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
