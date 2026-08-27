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
    public DateTime? ReadAt { get; private set; }
    public int? GroupId { get; private set; }
    public string? RelatedEntity { get; private set; }
    public int? RelatedId { get; private set; }
    public string? Link { get; private set; }
    public string Priority { get; private set; } = "normal";
    public string? DedupeKey { get; private set; }
    public bool IsArchived { get; private set; }

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
        DateTime utcNow,
        string? link = null,
        string? priority = null,
        string? dedupeKey = null)
    {
        return new AppNotification
        {
            UserId = userId,
            Title = title.Trim(),
            Message = message.Trim(),
            Type = type.Trim(),
            IsRead = false,
            CreatedAt = utcNow,
            GroupId = groupId,
            RelatedEntity = relatedEntity,
            RelatedId = relatedId,
            Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
            Priority = string.IsNullOrWhiteSpace(priority) ? "normal" : priority.Trim(),
            DedupeKey = string.IsNullOrWhiteSpace(dedupeKey) ? null : dedupeKey.Trim(),
            IsArchived = false
        };
    }

    public void MarkRead(DateTime utcNow)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = utcNow;
    }

    public void Archive() => IsArchived = true;
}
