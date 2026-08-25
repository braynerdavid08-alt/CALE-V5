using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Engagement.Application.Abstractions;
using Cale.Modules.Engagement.Domain;

namespace Cale.Modules.Engagement.Infrastructure;

public sealed class NotificationPublisher : INotificationPublisher, INotificationQueries
{
    private readonly INotificationStore _store;
    private readonly IClock _clock;

    public NotificationPublisher(INotificationStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public Task NotifyUserAsync(
        int userId,
        string title,
        string message,
        string type,
        int? groupId,
        string? relatedEntity,
        int? relatedId,
        CancellationToken ct) =>
        NotifyUsersAsync(
            [userId],
            title,
            message,
            type,
            groupId,
            relatedEntity,
            relatedId,
            ct);

    public async Task NotifyUsersAsync(
        IReadOnlyList<int> userIds,
        string title,
        string message,
        string type,
        int? groupId,
        string? relatedEntity,
        int? relatedId,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var items = userIds.Distinct().Select(id => AppNotification.Create(
            id,
            title,
            message,
            type,
            groupId,
            relatedEntity,
            relatedId,
            now)).ToList();
        if (items.Count == 0)
        {
            return;
        }

        await _store.AddRangeAsync(items, ct);
        await _store.SaveChangesAsync(ct);
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct) =>
        _store.CountUnreadAsync(userId, ct);
}
