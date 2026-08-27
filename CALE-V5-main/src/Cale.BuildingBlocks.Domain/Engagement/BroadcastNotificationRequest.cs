namespace Cale.BuildingBlocks.Domain.Engagement;

public sealed record BroadcastNotificationRequest(
    string Title,
    string Message,
    string Type,
    string? Priority,
    string? Link,
    string Audience,
    int? GroupId,
    IReadOnlyList<int>? UserIds);

public sealed record BroadcastNotificationResponse(
    int Sent,
    string Audience);
