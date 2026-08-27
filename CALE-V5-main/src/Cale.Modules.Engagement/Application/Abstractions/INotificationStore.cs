using Cale.Modules.Engagement.Domain;

namespace Cale.Modules.Engagement.Application.Abstractions;

public sealed record NotificationListQuery(
    int UserId,
    bool? UnreadOnly = null,
    string? Category = null,
    string? Type = null,
    int Skip = 0,
    int Take = 30);

public interface INotificationStore
{
    Task AddRangeAsync(
        IReadOnlyList<AppNotification> items,
        CancellationToken ct);

    Task<IReadOnlyList<AppNotification>> ListByUserAsync(
        NotificationListQuery query,
        CancellationToken ct);

    Task<AppNotification?> GetAsync(int id, CancellationToken ct);

    Task<int> CountUnreadAsync(int userId, CancellationToken ct);

    Task<int> MarkAllReadAsync(int userId, DateTime utcNow, CancellationToken ct);

    Task<HashSet<int>> FindUsersWithDedupeAsync(
        IReadOnlyList<int> userIds,
        string dedupeKey,
        CancellationToken ct);

    Task<NotificationPreference?> GetPreferenceAsync(
        int userId,
        CancellationToken ct);

    Task AddPreferenceAsync(NotificationPreference preference, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
