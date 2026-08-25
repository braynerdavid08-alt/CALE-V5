using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class ManageSchoolPlanHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IClock _clock;
    private readonly GetSchoolProfileHandler _profileQuery;

    public ManageSchoolPlanHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IClock clock,
        GetSchoolProfileHandler profileQuery)
    {
        _users = users;
        _profiles = profiles;
        _clock = clock;
        _profileQuery = profileQuery;
    }

    public async Task<SchoolProfileDto> SelectPlanAsync(
        int userId,
        ChangeSchoolPlanRequest request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadAsync(userId, ct);
        var plan = SchoolPlans.Find(request.PlanCode)
            ?? throw new DomainException("Plan inválido.", 400, "invalid_plan");

        profile.SelectPlan(plan);
        await _profiles.SaveChangesAsync(ct);
        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> ActivateOrRenewAsync(
        int userId,
        ActivateSchoolPlanRequest request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadAsync(userId, ct);
        var code = string.IsNullOrWhiteSpace(request.PlanCode)
            ? profile.PlanCode
            : request.PlanCode!;
        var plan = SchoolPlans.Find(code)
            ?? throw new DomainException("Plan inválido.", 400, "invalid_plan");

        profile.ActivateOrRenew(plan, _clock.UtcNow);
        await _profiles.SaveChangesAsync(ct);
        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> UpdateBillingAsync(
        int userId,
        UpdateSchoolBillingRequest request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(request.BillingEmail))
        {
            throw new DomainException(
                "Correo de facturación requerido.",
                400,
                "invalid_billing_email");
        }

        profile.UpdateBilling(
            request.LegalName,
            request.TaxId,
            EmailAddress.Normalize(request.BillingEmail),
            request.Phone,
            request.Address,
            request.City,
            request.Department);
        await _profiles.SaveChangesAsync(ct);
        return await _profileQuery.MapAsync(user, profile, ct);
    }

    private async Task<(User User, SchoolProfile Profile)> LoadAsync(
        int userId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        if (Roles.Normalize(user.Role) != Roles.School)
        {
            throw new ForbiddenException("Solo cuentas escuela pueden gestionar el plan.");
        }

        var profile = await _profiles.GetTrackedByUserIdAsync(userId, ct);
        if (profile is null)
        {
            var defaultPlan = SchoolPlans.Find(SchoolPlans.Monthly)!;
            profile = SchoolProfile.CreateDraft(
                user.Id,
                user.Name,
                user.Email,
                defaultPlan,
                _clock.UtcNow);
            await _profiles.AddAsync(profile, ct);
            await _profiles.SaveChangesAsync(ct);
        }

        profile.RefreshStatus(_clock.UtcNow);
        return (user, profile);
    }
}
