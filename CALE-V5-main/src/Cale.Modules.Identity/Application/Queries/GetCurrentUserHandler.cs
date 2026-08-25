using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Queries;

public sealed class GetCurrentUserHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IClock _clock;

    public GetCurrentUserHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<MeResponse> HandleAsync(int userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        var role = Roles.Normalize(user.Role);
        MeSchoolContextDto? school = null;

        if (role == Roles.School)
        {
            school = await MapSchoolAsync(user.Id, ct);
        }
        else if (user.SchoolId is not null)
        {
            school = await MapSchoolAsync(user.SchoolId.Value, ct);
        }

        return new MeResponse(
            user.Id,
            user.Name,
            user.Email,
            role,
            user.IsActive,
            user.CreatedAt,
            school);
    }

    private async Task<MeSchoolContextDto?> MapSchoolAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var profile = await _profiles.GetTrackedByUserIdAsync(schoolUserId, ct);
        if (profile is null)
        {
            var schoolUser = await _users.GetByIdAsync(schoolUserId, ct);
            if (schoolUser is null)
            {
                return null;
            }

            return new MeSchoolContextDto(
                schoolUserId,
                schoolUser.Name,
                "Sin plan",
                "",
                "",
                SchoolSubscriptionStatus.PendingPayment,
                0,
                false);
        }

        profile.RefreshStatus(_clock.UtcNow);
        await _profiles.SaveChangesAsync(ct);
        var plan = SchoolPlans.Find(profile.PlanCode);
        var days = profile.DaysRemaining(_clock.UtcNow);
        var active = profile.SubscriptionStatus == SchoolSubscriptionStatus.Active
            && days > 0;

        return new MeSchoolContextDto(
            schoolUserId,
            profile.LegalName,
            plan?.LabelEs ?? profile.PlanCode,
            profile.City,
            profile.Department,
            profile.SubscriptionStatus,
            days,
            active);
    }
}
