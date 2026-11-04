using Cale.BuildingBlocks.Domain.Access;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Services;

/// <summary>
/// Single source of truth for commercial directory / public presence eligibility.
/// Intentionally independent of <see cref="SchoolProfile.CanOperateProduct"/>:
/// FreeAccess and Courtesy never grant commercial presence.
/// </summary>
public interface ISchoolCommercialPresenceGate
{
    SchoolCommercialPresenceDecision Evaluate(
        SchoolProfile school,
        SchoolCommercialProfile? commercial,
        DateTime utcNow);
}

public sealed class SchoolCommercialPresenceGate : ISchoolCommercialPresenceGate
{
    public SchoolCommercialPresenceDecision Evaluate(
        SchoolProfile school,
        SchoolCommercialProfile? commercial,
        DateTime utcNow)
    {
        school.RefreshStatus(utcNow);

        // FreeAccess / Courtesy are operational-only; never consulted here.
        _ = FreeAccessPolicy.Enabled;

        if (school.IsPendingApproval)
        {
            return Deny(SchoolCommercialPresenceDenyReasons.PendingApproval);
        }

        if (school.SubscriptionStatus == SchoolSubscriptionStatus.Suspended)
        {
            return Deny(SchoolCommercialPresenceDenyReasons.MembershipSuspended);
        }

        var planCode = SchoolPlans.Normalize(school.PlanCode);
        if (planCode is not (SchoolPlans.Monthly or SchoolPlans.Annual))
        {
            return Deny(SchoolCommercialPresenceDenyReasons.PlanNotEligible);
        }

        if (school.PaymentAdminStatus != PaymentAdminStatuses.Confirmed)
        {
            return Deny(SchoolCommercialPresenceDenyReasons.PaymentNotConfirmed);
        }

        if (!school.IsCommerciallyActive(utcNow))
        {
            return Deny(SchoolCommercialPresenceDenyReasons.CoverageInactive);
        }

        if (!HasOperableContactSheet(school))
        {
            return Deny(SchoolCommercialPresenceDenyReasons.SchoolSheetIncomplete);
        }

        if (commercial is null || !commercial.IsSheetComplete)
        {
            return Deny(SchoolCommercialPresenceDenyReasons.CommercialSheetIncomplete);
        }

        if (commercial.IsCommerciallySuspended)
        {
            return Deny(SchoolCommercialPresenceDenyReasons.CommerciallySuspended);
        }

        if (!commercial.IsPublished)
        {
            return Deny(SchoolCommercialPresenceDenyReasons.NotPublished);
        }

        return SchoolCommercialPresenceDecision.Allow();
    }

    private static bool HasOperableContactSheet(SchoolProfile school) =>
        IsFilled(school.LegalName)
        && IsFilled(school.City)
        && IsFilled(school.Department)
        && IsFilled(school.Address)
        && IsFilled(school.Phone);

    private static bool IsFilled(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && !string.Equals(value.Trim(), SchoolProfile.NotRegistered, StringComparison.OrdinalIgnoreCase);

    private static SchoolCommercialPresenceDecision Deny(string reason) =>
        SchoolCommercialPresenceDecision.Deny(reason);
}

public sealed record SchoolCommercialPresenceDecision(bool Eligible, string? DenyReason)
{
    public static SchoolCommercialPresenceDecision Allow() => new(true, null);

    public static SchoolCommercialPresenceDecision Deny(string reason) => new(false, reason);
}

public static class SchoolCommercialPresenceDenyReasons
{
    public const string PendingApproval = "pending_approval";
    public const string MembershipSuspended = "membership_suspended";
    public const string PlanNotEligible = "plan_not_eligible";
    public const string PaymentNotConfirmed = "payment_not_confirmed";
    public const string CoverageInactive = "coverage_inactive";
    public const string SchoolSheetIncomplete = "school_sheet_incomplete";
    public const string CommercialSheetIncomplete = "commercial_sheet_incomplete";
    public const string CommerciallySuspended = "commercially_suspended";
    public const string NotPublished = "not_published";
}
