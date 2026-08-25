using Cale.BuildingBlocks.Domain.Classroom;
using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Classroom.Domain;

public sealed class ActivitySubmission
{
    public int Id { get; private set; }
    public int ActivityId { get; private set; }
    public int UserId { get; private set; }
    public string? TextContent { get; private set; }
    public string? FileUrl { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public decimal? Score { get; private set; }
    public string? TeacherComment { get; private set; }
    public string Status { get; private set; } = SubmissionStatuses.Submitted;

    private ActivitySubmission()
    {
    }

    public static ActivitySubmission Deliver(
        int activityId,
        int userId,
        string? text,
        string? fileUrl,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(fileUrl))
        {
            throw new DomainException(
                "Write a response or attach a file.",
                400,
                "empty_submission");
        }

        return new ActivitySubmission
        {
            ActivityId = activityId,
            UserId = userId,
            TextContent = text?.Trim(),
            FileUrl = fileUrl?.Trim(),
            SubmittedAt = utcNow,
            Status = SubmissionStatuses.Submitted
        };
    }

    public void Grade(decimal score, string? comment)
    {
        Score = score;
        TeacherComment = comment?.Trim();
        Status = SubmissionStatuses.Graded;
    }
}
