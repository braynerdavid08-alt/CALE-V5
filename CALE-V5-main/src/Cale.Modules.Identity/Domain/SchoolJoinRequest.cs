using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

public static class SchoolJoinRequestStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

/// <summary>Who started the link: the member asked to join, or the school invited the member.</summary>
public static class SchoolJoinDirections
{
    public const string Request = "Request";
    public const string Invite = "Invite";
}

/// <summary>
/// A pending link between a teacher/student account and a school. Nobody is linked until the other
/// side accepts: the school accepts a request, the member accepts an invitation.
/// </summary>
public sealed class SchoolJoinRequest
{
    public int Id { get; private set; }

    /// <summary>The teacher or student account (column kept from when only teachers could ask).</summary>
    public int TeacherUserId { get; private set; }
    public int SchoolUserId { get; private set; }
    public string Direction { get; private set; } = SchoolJoinDirections.Request;
    public string Status { get; private set; } = SchoolJoinRequestStatuses.Pending;
    public string? Message { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public int? DecidedByUserId { get; private set; }

    public int MemberUserId => TeacherUserId;
    public bool IsInvite => Direction == SchoolJoinDirections.Invite;

    private SchoolJoinRequest()
    {
    }

    public static SchoolJoinRequest Create(
        int memberUserId,
        int schoolUserId,
        string? message,
        DateTime utcNow,
        string direction = SchoolJoinDirections.Request)
    {
        var note = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (note is { Length: > 500 })
        {
            note = note[..500];
        }

        return new SchoolJoinRequest
        {
            TeacherUserId = memberUserId,
            SchoolUserId = schoolUserId,
            Direction = direction == SchoolJoinDirections.Invite ? SchoolJoinDirections.Invite : SchoolJoinDirections.Request,
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

    public void Cancel(int cancelledByUserId, DateTime utcNow)
    {
        EnsurePending();
        Status = SchoolJoinRequestStatuses.Cancelled;
        DecidedAt = utcNow;
        DecidedByUserId = cancelledByUserId;
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
