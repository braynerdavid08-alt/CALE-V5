namespace Cale.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Draft used by the central notification publisher.
/// </summary>
public sealed record NotificationDraft(
    string Title,
    string Message,
    string Type,
    int? GroupId = null,
    string? RelatedEntity = null,
    int? RelatedId = null,
    string? Link = null,
    string? Priority = null,
    string? DedupeKey = null);

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

    Task NotifyUsersAsync(
        IReadOnlyList<int> userIds,
        NotificationDraft draft,
        CancellationToken ct);
}
