using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Queries;

public sealed class GetSchoolProfileHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IClock _clock;

    public GetSchoolProfileHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<SchoolProfileDto> HandleAsync(int userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        if (Roles.Normalize(user.Role) != Roles.School)
        {
            throw new ForbiddenException("Solo cuentas escuela pueden ver este perfil.");
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
        await _profiles.SaveChangesAsync(ct);

        return await MapAsync(user, profile, ct);
    }

    internal async Task<SchoolProfileDto> MapAsync(
        User user,
        SchoolProfile profile,
        CancellationToken ct)
    {
        var plan = SchoolPlans.Find(profile.PlanCode);
        var requestedPlan = SchoolPlans.Find(profile.EffectiveRequestedPlanCode);
        var teachersUsed = await _users.CountBySchoolAndRoleAsync(
            user.Id, Roles.Teacher, ct);
        var studentsUsed = await _users.CountBySchoolAndRoleAsync(
            user.Id, Roles.Student, ct);
        var days = profile.DaysRemaining(_clock.UtcNow);
        var now = _clock.UtcNow;
        var active = profile.IsCommerciallyActive(now);

        return new SchoolProfileDto(
            user.Id,
            user.Name,
            user.Email,
            profile.LegalName,
            profile.TaxId,
            profile.BillingEmail,
            profile.Phone,
            profile.Address,
            profile.City,
            profile.Department,
            profile.PlanCode,
            plan?.LabelEs ?? profile.PlanCode,
            profile.PlanPriceCop,
            plan?.MonthlyEquivalentCop ?? 0,
            plan?.DurationMonths ?? 0,
            profile.SubscriptionStatus,
            profile.DisplayStatus(now),
            profile.RenewalStatus,
            profile.CreatedAt,
            profile.MembershipStartsAt,
            profile.MembershipEndsAt,
            days,
            active,
            profile.RequestedPlanCode,
            requestedPlan?.LabelEs ?? profile.RequestedPlanCode,
            profile.HasOpenCommercialRequest(now),
            profile.NeedsPaymentProof(now),
            profile.AwaitingAdminReview(now),
            profile.PaymentProofUrl,
            profile.PaymentReference,
            profile.RejectionReason,
            profile.SuspensionReason,
            profile.RequestedAt,
            profile.ProofSubmittedAt,
            profile.LastDecisionAt,
            new SchoolPaymentInstructionsDto(
                SchoolPaymentInstructions.BankName,
                SchoolPaymentInstructions.AccountType,
                SchoolPaymentInstructions.AccountNumber,
                SchoolPaymentInstructions.AccountHolder,
                SchoolPaymentInstructions.HolderTaxId,
                SchoolPaymentInstructions.WhatsApp,
                SchoolPaymentInstructions.SupportEmail,
                SchoolPaymentInstructions.Notes,
                $"Ref: {profile.TaxId} / {user.Email}"),
            teachersUsed,
            profile.EffectiveMaxTeachers(plan),
            studentsUsed,
            profile.EffectiveMaxStudents(plan));
    }
}

public sealed class ListSchoolPlansHandler
{
    public IReadOnlyList<SchoolPlanDto> Handle(bool includeTrial = false) =>
        SchoolPlans.All
            .Where(x => includeTrial || x.Code != SchoolPlans.Trial)
            .Select(x => new SchoolPlanDto(
                x.Code,
                x.LabelEs,
                x.PriceCop,
                x.MonthlyEquivalentCop,
                x.DurationMonths,
                x.MaxTeachers,
                x.MaxStudents))
            .ToList();
}

public sealed class ListSchoolMembersHandler
{
    private readonly IUserStore _users;

    public ListSchoolMembersHandler(IUserStore users) => _users = users;

    public async Task<IReadOnlyList<UserListItemDto>> HandleAsync(
        int schoolId,
        CancellationToken ct)
    {
        var members = await _users.ListBySchoolAsync(schoolId, ct);
        return members
            .Where(x =>
                Roles.Normalize(x.Role) is Roles.Teacher or Roles.Student)
            .Select(user => new UserListItemDto(
                user.Id,
                user.Name,
                user.Email,
                Roles.Normalize(user.Role),
                user.IsActive,
                user.CreatedAt,
                user.LastLoginAt))
            .ToList();
    }
}
