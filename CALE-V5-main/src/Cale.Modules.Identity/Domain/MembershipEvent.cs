namespace Cale.Modules.Identity.Domain;

public sealed class MembershipEvent
{
    public int Id { get; private set; }
    public int SchoolUserId { get; private set; }
    public string EventType { get; private set; } = "";
    public string? PlanCode { get; private set; }
    public decimal? PlanPriceCop { get; private set; }
    public int? ActorUserId { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private MembershipEvent()
    {
    }

    public static MembershipEvent Create(
        int schoolUserId,
        string eventType,
        string? planCode,
        decimal? planPriceCop,
        int? actorUserId,
        string? note,
        DateTime utcNow)
    {
        return new MembershipEvent
        {
            SchoolUserId = schoolUserId,
            EventType = eventType,
            PlanCode = planCode,
            PlanPriceCop = planPriceCop,
            ActorUserId = actorUserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = utcNow
        };
    }
}

public static class MembershipEventTypes
{
    public const string Requested = "Requested";
    public const string ProofSubmitted = "ProofSubmitted";
    public const string Activated = "Activated";
    public const string FreeTrialActivated = "FreeTrialActivated";
    public const string Renewed = "Renewed";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
    public const string Suspended = "Suspended";
    public const string Unsuspended = "Unsuspended";
    public const string SeatsAdjusted = "SeatsAdjusted";
    public const string MembershipOverridden = "MembershipOverridden";
    public const string RequestReopened = "RequestReopened";
    public const string MemberCreated = "MemberCreated";
    public const string MemberAttached = "MemberAttached";
    public const string MemberUpdated = "MemberUpdated";
    public const string MemberImported = "MemberImported";
}
