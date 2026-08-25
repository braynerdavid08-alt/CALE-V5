using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Classroom.Domain;

public sealed class Announcement
{
    public int Id { get; private set; }
    public int GroupId { get; private set; }
    public int AuthorId { get; private set; }
    public string Title { get; private set; } = "";
    public string Body { get; private set; } = "";
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Announcement()
    {
    }

    public static Announcement Publish(
        int groupId,
        int authorId,
        string title,
        string body,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
        {
            throw new DomainException(
                "Title and content are required.",
                400,
                "invalid_announcement");
        }

        return new Announcement
        {
            GroupId = groupId,
            AuthorId = authorId,
            Title = title.Trim(),
            Body = body.Trim(),
            IsActive = true,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }
}
