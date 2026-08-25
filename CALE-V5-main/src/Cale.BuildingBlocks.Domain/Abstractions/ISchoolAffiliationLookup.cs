namespace Cale.BuildingBlocks.Domain.Abstractions;

public interface ISchoolAffiliationLookup
{
    Task<SchoolAffiliationSnapshot?> GetForMemberAsync(
        int userId,
        CancellationToken ct = default);
}

public sealed record SchoolAffiliationSnapshot(
    int SchoolId,
    string LegalName,
    string PlanLabel,
    string City,
    string Department,
    string SubscriptionStatus,
    int DaysRemaining,
    bool IsMembershipActive);
