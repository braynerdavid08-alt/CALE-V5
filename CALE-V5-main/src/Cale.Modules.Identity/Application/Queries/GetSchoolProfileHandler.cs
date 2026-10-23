using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;
using Microsoft.Extensions.Options;

namespace Cale.Modules.Identity.Application.Queries;

public sealed class GetSchoolProfileHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly ICommercialPlanStore _plans;
    private readonly IClock _clock;
    private readonly SchoolPaymentOptions _payment;

    public GetSchoolProfileHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        ICommercialPlanStore plans,
        IClock clock,
        IOptions<SchoolPaymentOptions> payment)
    {
        _users = users;
        _profiles = profiles;
        _plans = plans;
        _clock = clock;
        _payment = payment.Value;
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
        var plan = await _plans.ResolvePlanInfoAsync(profile.PlanCode, ct)
            ?? SchoolPlans.Find(profile.PlanCode);
        var requestedPlan = await _plans.ResolvePlanInfoAsync(profile.EffectiveRequestedPlanCode, ct)
            ?? SchoolPlans.Find(profile.EffectiveRequestedPlanCode);
        var teachersUsed = await _users.CountBySchoolAndRoleAsync(
            user.Id, Roles.Teacher, ct);
        var studentsUsed = await _users.CountBySchoolAndRoleAsync(
            user.Id, Roles.Student, ct);
        var days = profile.DaysRemaining(_clock.UtcNow);
        var now = _clock.UtcNow;
        var active = profile.CanOperateProduct(now);

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
            profile.EffectiveAgreedPriceCop,
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
            PaymentInstructions(profile, user),
            teachersUsed,
            profile.EffectiveMaxTeachers(plan),
            studentsUsed,
            profile.EffectiveMaxStudents(plan),
            profile.IsPendingApproval,
            profile.PaymentAdminStatus,
            profile.AgreedPriceCop,
            profile.CourtesyUntil,
            profile.CourtesyReason,
            profile.IsCourtesyActive(now));
    }

    private SchoolPaymentInstructionsDto PaymentInstructions(SchoolProfile profile, User user)
    {
        if (!_payment.IsConfigured)
        {
            return new SchoolPaymentInstructionsDto(
                "", "", "", "", "", "", "",
                SchoolPaymentInstructions.NotConfiguredNotes,
                "",
                Configured: false);
        }

        return new SchoolPaymentInstructionsDto(
            _payment.BankName!.Trim(),
            _payment.AccountType?.Trim() ?? "",
            _payment.AccountNumber!.Trim(),
            _payment.AccountHolder!.Trim(),
            _payment.HolderTaxId!.Trim(),
            _payment.WhatsApp?.Trim() ?? "",
            _payment.SupportEmail?.Trim() ?? "",
            SchoolPaymentInstructions.Notes,
            $"Ref: {profile.TaxId} / {user.Email}",
            Configured: true);
    }
}

public sealed class ListSchoolPlansHandler
{
    private readonly ICommercialPlanStore _plans;

    public ListSchoolPlansHandler(ICommercialPlanStore plans) => _plans = plans;

    public IReadOnlyList<SchoolPlanDto> Handle() =>
        SchoolPlans.CommercialOffer
            .Select(MapFallback)
            .ToList();

    public async Task<IReadOnlyList<SchoolPlanDto>> HandleAsync(CancellationToken ct)
    {
        var rows = await _plans.ListAsync(includeInactive: false, ct);
        if (rows.Count == 0)
        {
            return Handle();
        }

        return rows
            .Where(x => !x.IsLegacy)
            .Select(x =>
            {
                var info = x.ToPlanInfo();
                return new SchoolPlanDto(
                    info.Code,
                    info.LabelEs,
                    info.PriceCop,
                    info.MonthlyEquivalentCop,
                    info.DurationMonths,
                    info.MaxTeachers,
                    info.MaxStudents,
                    x.IsActive,
                    x.IsLegacy,
                    UnlimitedSeats: !SchoolPlans.HasSeatCap(info.MaxStudents)
                        && !SchoolPlans.HasSeatCap(info.MaxTeachers));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SchoolPlanDto>> ListAllForAdminAsync(CancellationToken ct)
    {
        var rows = await _plans.ListAsync(includeInactive: true, ct);
        if (rows.Count == 0)
        {
            return SchoolPlans.All.Select(MapFallback).ToList();
        }

        return rows.Select(x =>
        {
            var info = x.ToPlanInfo();
            return new SchoolPlanDto(
                info.Code,
                info.LabelEs,
                info.PriceCop,
                info.MonthlyEquivalentCop,
                info.DurationMonths,
                info.MaxTeachers,
                info.MaxStudents,
                x.IsActive,
                x.IsLegacy,
                UnlimitedSeats: !SchoolPlans.HasSeatCap(info.MaxStudents)
                    && !SchoolPlans.HasSeatCap(info.MaxTeachers));
        }).ToList();
    }

    private static SchoolPlanDto MapFallback(SchoolPlanInfo x) =>
        new(
            x.Code,
            x.LabelEs,
            x.PriceCop,
            x.MonthlyEquivalentCop,
            x.DurationMonths,
            x.MaxTeachers,
            x.MaxStudents,
            IsActive: x.Code is not SchoolPlans.Semestral,
            IsLegacy: x.Code is SchoolPlans.Semestral,
            UnlimitedSeats: !SchoolPlans.HasSeatCap(x.MaxStudents)
                && !SchoolPlans.HasSeatCap(x.MaxTeachers));
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
