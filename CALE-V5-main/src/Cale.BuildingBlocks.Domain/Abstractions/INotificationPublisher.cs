namespace Cale.BuildingBlocks.Domain.Abstractions;

public interface INotificationPublisher
{
    Task NotifyUserAsync(
        int userId,
        string title,
        string message,
        string type,
        int? groupId,
        string? relatedEntity,
        int? relatedId,
        CancellationToken ct);

    Task NotifyUsersAsync(
        IReadOnlyList<int> userIds,
        string title,
        string message,
        string type,
        int? groupId,
        string? relatedEntity,
        int? relatedId,
        CancellationToken ct);
}
