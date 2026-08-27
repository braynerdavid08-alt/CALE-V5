namespace Cale.Modules.Engagement.Application.DTOs;

public sealed record NotificationDto(
    int Id,
    string Title,
    string Message,
    string Type,
    string Category,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt,
    int? GroupId,
    string? RelatedEntity,
    int? RelatedId,
    string? Link,
    string Priority);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationDto> Items,
    int UnreadCount);

public sealed record NotificationPreferenceDto(
    bool AcademicEnabled,
    bool MembershipEnabled,
    bool AdminEnabled,
    bool SystemEnabled);

public sealed record UpdateNotificationPreferenceRequest(
    bool AcademicEnabled,
    bool MembershipEnabled,
    bool AdminEnabled,
    bool SystemEnabled);
