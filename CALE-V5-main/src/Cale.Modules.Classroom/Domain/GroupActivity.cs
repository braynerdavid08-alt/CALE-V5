using Cale.BuildingBlocks.Domain.Classroom;
using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Classroom.Domain;

public sealed class GroupActivity
{
    public int Id { get; private set; }
    public int GroupId { get; private set; }
    public int AuthorId { get; private set; }
    public string Type { get; private set; } = ActivityTypes.Activity;
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string? Instructions { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public DateTime? DueAt { get; private set; }
    public decimal? MaxScore { get; private set; }
    public string? AttachmentUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private GroupActivity()
    {
    }

    public static GroupActivity Publish(
        int groupId,
        int authorId,
        string type,
        string title,
        string description,
        string? instructions,
        DateTime? dueAt,
        decimal? maxScore,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Activity title is required.", 400, "invalid_title");
        }

        var normalized = string.IsNullOrWhiteSpace(type)
            ? ActivityTypes.Activity
            : type.Trim().ToLowerInvariant();

        return new GroupActivity
        {
            GroupId = groupId,
            AuthorId = authorId,
            Type = normalized,
            Title = title.Trim(),
            Description = description?.Trim() ?? "",
            Instructions = instructions?.Trim(),
            PublishedAt = utcNow,
            DueAt = dueAt,
            MaxScore = maxScore,
            IsActive = true,
            CreatedAt = utcNow
        };
    }

    public string ResolveStatus(DateTime utcNow, string? submissionStatus)
    {
        if (string.Equals(submissionStatus, SubmissionStatuses.Graded, StringComparison.OrdinalIgnoreCase))
        {
            return ItemStatuses.Graded;
        }

        if (string.Equals(submissionStatus, SubmissionStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            return ItemStatuses.Submitted;
        }

        if (DueAt is { } due && utcNow > due)
        {
            return ItemStatuses.Expired;
        }

        return ItemStatuses.Available;
    }
}
