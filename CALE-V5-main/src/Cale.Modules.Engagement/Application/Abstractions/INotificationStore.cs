using Cale.Modules.Engagement.Domain;

namespace Cale.Modules.Engagement.Application.Abstractions;

public interface INotificationStore
{
    Task AddRangeAsync(
        IReadOnlyList<AppNotification> items,
        CancellationToken ct);

    Task<IReadOnlyList<AppNotification>> ListByUserAsync(
        int userId,
        CancellationToken ct);

    Task<AppNotification?> GetAsync(int id, CancellationToken ct);
    Task<int> CountUnreadAsync(int userId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
