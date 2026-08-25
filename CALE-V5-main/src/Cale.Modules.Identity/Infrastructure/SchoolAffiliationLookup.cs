using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Infrastructure;

public sealed class SchoolAffiliationLookup : ISchoolAffiliationLookup
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IClock _clock;

    public SchoolAffiliationLookup(
        IUserStore users,
        ISchoolProfileStore profiles,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<SchoolAffiliationSnapshot?> GetForMemberAsync(
        int userId,
        CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user?.SchoolId is null)
        {
            return null;
        }

        var schoolId = user.SchoolId.Value;
        var profile = await _profiles.GetByUserIdAsync(schoolId, ct);
        if (profile is null)
        {
            var schoolUser = await _users.GetByIdAsync(schoolId, ct);
            if (schoolUser is null)
            {
                return null;
            }

            return new SchoolAffiliationSnapshot(
                schoolId,
                schoolUser.Name,
                "Sin plan",
                "",
                "",
                SchoolSubscriptionStatus.PendingPayment,
                0,
                false);
        }

        profile.RefreshStatus(_clock.UtcNow);
        var plan = SchoolPlans.Find(profile.PlanCode);
        var days = profile.DaysRemaining(_clock.UtcNow);
        var active = profile.SubscriptionStatus == SchoolSubscriptionStatus.Active
            && days > 0;

        return new SchoolAffiliationSnapshot(
            schoolId,
            profile.LegalName,
            plan?.LabelEs ?? profile.PlanCode,
            profile.City,
            profile.Department,
            profile.SubscriptionStatus,
            days,
            active);
    }
}
