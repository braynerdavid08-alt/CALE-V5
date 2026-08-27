using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

public static class SchoolJoinRequestStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public sealed class SchoolJoinRequest
{
    public int Id { get; private set; }
    public int TeacherUserId { get; private set; }
    public int SchoolUserId { get; private set; }
    public string Status { get; private set; } = SchoolJoinRequestStatuses.Pending;
    public string? Message { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public int? DecidedByUserId { get; private set; }

    private SchoolJoinRequest()
    {
    }

    public static SchoolJoinRequest Create(
        int teacherUserId,
        int schoolUserId,
        string? message,
        DateTime utcNow)
    {
        var note = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (note is { Length: > 500 })
        {
            note = note[..500];
        }

        return new SchoolJoinRequest
        {
            TeacherUserId = teacherUserId,
            SchoolUserId = schoolUserId,
            Status = SchoolJoinRequestStatuses.Pending,
            Message = note,
            CreatedAt = utcNow
        };
    }

    public void Accept(int decidedByUserId, DateTime utcNow)
    {
        EnsurePending();
        Status = SchoolJoinRequestStatuses.Accepted;
        DecidedAt = utcNow;
        DecidedByUserId = decidedByUserId;
    }

    public void Reject(int decidedByUserId, string? reason, DateTime utcNow)
    {
        EnsurePending();
        Status = SchoolJoinRequestStatuses.Rejected;
        DecidedAt = utcNow;
        DecidedByUserId = decidedByUserId;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (RejectionReason is { Length: > 500 })
        {
            RejectionReason = RejectionReason[..500];
        }
    }

    public void Cancel(DateTime utcNow)
    {
        EnsurePending();
        Status = SchoolJoinRequestStatuses.Cancelled;
        DecidedAt = utcNow;
        DecidedByUserId = TeacherUserId;
    }

    private void EnsurePending()
    {
        if (Status != SchoolJoinRequestStatuses.Pending)
        {
            throw new DomainException(
                "La solicitud ya fue resuelta.",
                400,
                "join_request_closed");
        }
    }
}
