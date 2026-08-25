namespace Cale.Modules.Engagement.Domain;

public sealed class AppNotification
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string Title { get; private set; } = "";
    public string Message { get; private set; } = "";
    public string Type { get; private set; } = "";
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int? GroupId { get; private set; }
    public string? RelatedEntity { get; private set; }
    public int? RelatedId { get; private set; }

    private AppNotification()
    {
    }

    public static AppNotification Create(
        int userId,
        string title,
        string message,
        string type,
        int? groupId,
        string? relatedEntity,
        int? relatedId,
        DateTime utcNow)
    {
        return new AppNotification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = utcNow,
            GroupId = groupId,
            RelatedEntity = relatedEntity,
            RelatedId = relatedId
        };
    }

    public void MarkRead() => IsRead = true;
}
