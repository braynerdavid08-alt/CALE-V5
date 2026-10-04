namespace Cale.Modules.Identity.Domain;

/// <summary>Historial de suspensiones y reactivaciones de una cuenta. Nunca se borra ni se edita.</summary>
public sealed class AccountStatusEvent
{
    public const int ReasonMax = 500;
    public const int EvidenceMax = 1000;
    public const int ReasonMin = 10;
    public const int MaxDurationDays = 3650;

    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string Action { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public string? Evidence { get; private set; }
    public DateTime? SuspendedUntil { get; private set; }
    public int? ActorUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AccountStatusEvent()
    {
    }

    public static AccountStatusEvent Suspend(
        int userId,
        string reason,
        string? evidence,
        DateTime? suspendedUntil,
        int actorUserId,
        DateTime utcNow) =>
        new()
        {
            UserId = userId,
            Action = AccountStatusActions.Suspended,
            Reason = reason.Trim(),
            Evidence = string.IsNullOrWhiteSpace(evidence) ? null : evidence.Trim(),
            SuspendedUntil = suspendedUntil,
            ActorUserId = actorUserId,
            CreatedAt = utcNow
        };

    public static AccountStatusEvent Reactivate(
        int userId,
        string reason,
        int? actorUserId,
        DateTime utcNow) =>
        new()
        {
            UserId = userId,
            Action = AccountStatusActions.Reactivated,
            Reason = reason.Trim(),
            ActorUserId = actorUserId,
            CreatedAt = utcNow
        };

    public bool IsSuspension => Action == AccountStatusActions.Suspended;

    public bool HasExpired(DateTime utcNow) =>
        IsSuspension && SuspendedUntil is { } until && until <= utcNow;
}

public static class AccountStatusActions
{
    public const string Suspended = "Suspended";
    public const string Reactivated = "Reactivated";
}
