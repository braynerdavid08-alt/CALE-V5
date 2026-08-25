namespace Cale.Modules.Engagement.Application.DTOs;

public sealed record NotificationDto(
    int Id,
    string Title,
    string Message,
    string Type,
    bool IsRead,
    DateTime CreatedAt,
    int? GroupId);
