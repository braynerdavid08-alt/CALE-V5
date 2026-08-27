using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
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
    private readonly IMembershipEventStore _events;
    private readonly INotificationPublisher _notifications;
    private readonly IClock _clock;
    private readonly GetSchoolProfileHandler _profileQuery;

    public ManageSchoolPlanHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IMembershipEventStore events,
        INotificationPublisher notifications,
        IClock clock,
        GetSchoolProfileHandler profileQuery)
    {
        _users = users;
        _profiles = profiles;
        _events = events;
        _notifications = notifications;
        _clock = clock;
        _profileQuery = profileQuery;
    }

    public async Task<SchoolProfileDto> RequestMembershipAsync(
        int schoolUserId,
        RequestSchoolMembershipRequest request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        var code = string.IsNullOrWhiteSpace(request.PlanCode)
            ? profile.EffectiveRequestedPlanCode
            : request.PlanCode!;
        var plan = SchoolPlans.Find(code)
            ?? throw new DomainException("Plan inválido.", 400, "invalid_plan");

        profile.RequestMembership(plan, _clock.UtcNow);
        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                MembershipEventTypes.Requested,
                plan.Code,
                plan.PriceCop,
                schoolUserId,
                null,
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);
        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> SubmitPaymentProofAsync(
        int schoolUserId,
        SubmitPaymentProofRequest request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        profile.SubmitPaymentProof(
            request.PaymentProofUrl,
            request.PaymentReference,
            _clock.UtcNow);

        var planCode = profile.EffectiveRequestedPlanCode;
        var plan = SchoolPlans.Find(planCode);
        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                MembershipEventTypes.ProofSubmitted,
                planCode,
                plan?.PriceCop ?? profile.PlanPriceCop,
                schoolUserId,
                request.PaymentReference,
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        var admins = await _users.ListByRoleAsync(Roles.Admin, ct);
        if (admins.Count > 0)
        {
            await _notifications.NotifyUsersAsync(
                admins.Select(a => a.Id).ToList(),
                "Comprobante de membresía",
                $"{profile.LegalName} subió un comprobante para el plan {plan?.LabelEs ?? planCode}.",
                NotificationTypes.Membership,
                null,
                "admin_review",
                schoolUserId,
                ct);
        }

        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> UpdateBillingAsync(
        int schoolUserId,
        UpdateSchoolBillingRequest request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
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

    public async Task<IReadOnlyList<MembershipEventDto>> ListHistoryAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        _ = await LoadSchoolAsync(schoolUserId, ct);
        var events = await _events.ListBySchoolAsync(schoolUserId, ct);
        return events
            .Select(e => new MembershipEventDto(
                e.Id,
                e.EventType,
                e.PlanCode,
                e.PlanPriceCop,
                e.Note,
                e.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<SchoolMembershipRequestDto>> ListPendingAsync(
        CancellationToken ct)
    {
        var all = await _profiles.ListAllAsync(ct);
        var now = _clock.UtcNow;
        var items = new List<SchoolMembershipRequestDto>();

        foreach (var snapshot in all)
        {
            var profile = await _profiles.GetTrackedByUserIdAsync(snapshot.UserId, ct);
            if (profile is null || !profile.HasOpenCommercialRequest(now))
            {
                continue;
            }

            var user = await _users.GetByIdAsync(profile.UserId, ct);
            if (user is null || Roles.Normalize(user.Role) != Roles.School)
            {
                continue;
            }

            var requestedCode = profile.EffectiveRequestedPlanCode;
            var plan = SchoolPlans.Find(requestedCode);
            var activePlan = SchoolPlans.Find(profile.PlanCode);
            var isRenewal = profile.IsCommerciallyActive(now)
                && profile.RenewalStatus != SchoolRenewalStatus.None;
            var teachersUsed = await _users.CountBySchoolAndRoleAsync(
                profile.UserId, Roles.Teacher, ct);
            var studentsUsed = await _users.CountBySchoolAndRoleAsync(
                profile.UserId, Roles.Student, ct);

            items.Add(new SchoolMembershipRequestDto(
                profile.UserId,
                user.Name,
                user.Email,
                profile.LegalName,
                profile.TaxId,
                profile.BillingEmail,
                profile.Phone,
                profile.City,
                profile.Department,
                requestedCode,
                plan?.LabelEs ?? requestedCode,
                plan?.PriceCop ?? profile.PlanPriceCop,
                plan?.DurationMonths ?? 0,
                profile.SubscriptionStatus,
                profile.DisplayStatus(now),
                profile.RenewalStatus,
                profile.RequestedPlanCode,
                isRenewal,
                !string.IsNullOrWhiteSpace(profile.PaymentProofUrl),
                profile.PaymentProofUrl,
                profile.PaymentReference,
                profile.RequestedAt,
                profile.ProofSubmittedAt,
                profile.CreatedAt,
                profile.MembershipStartsAt,
                profile.MembershipEndsAt,
                teachersUsed,
                profile.EffectiveMaxTeachers(activePlan),
                studentsUsed,
                profile.EffectiveMaxStudents(activePlan),
                profile.TeachersMaxOverride,
                profile.StudentsMaxOverride,
                profile.RejectionReason,
                profile.SuspensionReason));
        }

        await _profiles.SaveChangesAsync(ct);
        return items
            .OrderByDescending(x => x.HasPaymentProof)
            .ThenByDescending(x => x.ProofSubmittedAt ?? x.RequestedAt ?? x.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<AdminSchoolSummaryDto>> ListSchoolsAsync(
        CancellationToken ct)
    {
        var all = await _profiles.ListAllAsync(ct);
        var now = _clock.UtcNow;
        var items = new List<AdminSchoolSummaryDto>();

        foreach (var snapshot in all)
        {
            var profile = await _profiles.GetTrackedByUserIdAsync(snapshot.UserId, ct);
            if (profile is null)
            {
                continue;
            }

            profile.RefreshStatus(now);
            var user = await _users.GetByIdAsync(profile.UserId, ct);
            if (user is null || Roles.Normalize(user.Role) != Roles.School)
            {
                continue;
            }

            var plan = SchoolPlans.Find(profile.PlanCode);
            var teachersUsed = await _users.CountBySchoolAndRoleAsync(
                profile.UserId, Roles.Teacher, ct);
            var studentsUsed = await _users.CountBySchoolAndRoleAsync(
                profile.UserId, Roles.Student, ct);

            items.Add(new AdminSchoolSummaryDto(
                profile.UserId,
                user.Name,
                user.Email,
                profile.LegalName,
                profile.TaxId,
                profile.PlanCode,
                plan?.LabelEs ?? profile.PlanCode,
                profile.SubscriptionStatus,
                profile.DisplayStatus(now),
                profile.RenewalStatus,
                profile.IsCommerciallyActive(now),
                profile.DaysRemaining(now),
                profile.MembershipEndsAt,
                teachersUsed,
                profile.EffectiveMaxTeachers(plan),
                studentsUsed,
                profile.EffectiveMaxStudents(plan),
                profile.HasSeatOverrides,
                profile.HasOpenCommercialRequest(now),
                profile.CreatedAt));
        }

        await _profiles.SaveChangesAsync(ct);
        return items
            .OrderByDescending(x => x.HasOpenRequest)
            .ThenBy(x => x.LegalName)
            .ToList();
    }

    public async Task<AdminSchoolDetailDto> GetSchoolDetailAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        var now = _clock.UtcNow;
        profile.RefreshStatus(now);
        await _profiles.SaveChangesAsync(ct);

        var plan = SchoolPlans.Find(profile.PlanCode);
        var teachersUsed = await _users.CountBySchoolAndRoleAsync(
            schoolUserId, Roles.Teacher, ct);
        var studentsUsed = await _users.CountBySchoolAndRoleAsync(
            schoolUserId, Roles.Student, ct);

        var members = (await _users.ListBySchoolAsync(schoolUserId, ct))
            .Where(m => Roles.Normalize(m.Role) is Roles.Teacher or Roles.Student)
            .OrderBy(m => Roles.Normalize(m.Role))
            .ThenBy(m => m.Name)
            .Select(m => new UserListItemDto(
                m.Id,
                m.Name,
                m.Email,
                Roles.Normalize(m.Role),
                m.IsActive,
                m.CreatedAt,
                m.LastLoginAt))
            .ToList();

        var history = (await _events.ListBySchoolAsync(schoolUserId, ct))
            .Select(e => new MembershipEventDto(
                e.Id,
                e.EventType,
                e.PlanCode,
                e.PlanPriceCop,
                e.Note,
                e.CreatedAt))
            .ToList();

        return new AdminSchoolDetailDto(
            profile.UserId,
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
            profile.SubscriptionStatus,
            profile.DisplayStatus(now),
            profile.RenewalStatus,
            profile.IsCommerciallyActive(now),
            profile.DaysRemaining(now),
            profile.MembershipStartsAt,
            profile.MembershipEndsAt,
            teachersUsed,
            profile.EffectiveMaxTeachers(plan),
            studentsUsed,
            profile.EffectiveMaxStudents(plan),
            profile.HasSeatOverrides,
            profile.HasOpenCommercialRequest(now),
            profile.CreatedAt,
            members,
            history);
    }

    public async Task<SchoolProfileDto> AdminSetSeatsAsync(
        int schoolUserId,
        int adminUserId,
        AdminSetSchoolSeatsRequest request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        profile.SetSeatOverrides(request.TeachersMax, request.StudentsMax, _clock.UtcNow);
        var note = string.IsNullOrWhiteSpace(request.Note)
            ? $"Cupos: docentes={request.TeachersMax?.ToString() ?? "plan"}, estudiantes={request.StudentsMax?.ToString() ?? "plan"}"
            : request.Note!;
        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                MembershipEventTypes.SeatsAdjusted,
                profile.PlanCode,
                profile.PlanPriceCop,
                adminUserId,
                note,
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);
        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> AdminOverrideMembershipAsync(
        int schoolUserId,
        int adminUserId,
        AdminOverrideSchoolMembershipRequest request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        SchoolPlanInfo? plan = null;
        if (!string.IsNullOrWhiteSpace(request.PlanCode))
        {
            plan = SchoolPlans.Find(request.PlanCode)
                ?? throw new DomainException("Plan inválido.", 400, "invalid_plan");
        }

        profile.AdminOverrideMembership(
            plan,
            request.SubscriptionStatus,
            request.MembershipEndsAt,
            request.ClearRejection,
            _clock.UtcNow);

        var note = string.IsNullOrWhiteSpace(request.Note)
            ? $"Override → {profile.SubscriptionStatus} / {profile.PlanCode}"
            : request.Note!;
        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                MembershipEventTypes.MembershipOverridden,
                profile.PlanCode,
                profile.PlanPriceCop,
                adminUserId,
                note,
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        await _notifications.NotifyUserAsync(
            schoolUserId,
            "Membresía actualizada por administración",
            note,
            NotificationTypes.Membership,
            null,
            NotificationTypes.Membership,
            schoolUserId,
            ct);

        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> AdminReopenAsync(
        int schoolUserId,
        int adminUserId,
        AdminReopenSchoolRequest? request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        SchoolPlanInfo? plan = null;
        if (!string.IsNullOrWhiteSpace(request?.PlanCode))
        {
            plan = SchoolPlans.Find(request.PlanCode)
                ?? throw new DomainException("Plan inválido.", 400, "invalid_plan");
        }

        profile.AdminReopenRequest(plan, _clock.UtcNow);
        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                MembershipEventTypes.RequestReopened,
                profile.EffectiveRequestedPlanCode,
                profile.PlanPriceCop,
                adminUserId,
                request?.Note ?? "Solicitud reabierta para revisión",
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);
        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> AdminActivateAsync(
        int schoolUserId,
        int adminUserId,
        ActivateSchoolPlanRequest? request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        var code = string.IsNullOrWhiteSpace(request?.PlanCode)
            ? profile.EffectiveRequestedPlanCode
            : request!.PlanCode!;
        var plan = SchoolPlans.Find(code)
            ?? throw new DomainException("Plan inválido.", 400, "invalid_plan");

        if (!profile.HasOpenCommercialRequest(_clock.UtcNow)
            && profile.SubscriptionStatus != SchoolSubscriptionStatus.Expired
            && profile.SubscriptionStatus != SchoolSubscriptionStatus.Rejected
            && profile.SubscriptionStatus != SchoolSubscriptionStatus.Cancelled
            && profile.SubscriptionStatus != SchoolSubscriptionStatus.None
            && !(request?.ForceWithoutProof ?? false))
        {
            throw new DomainException(
                "No hay solicitud de membresía para activar.",
                400,
                "no_membership_request");
        }

        if (string.IsNullOrWhiteSpace(profile.PaymentProofUrl)
            && !(request?.ForceWithoutProof ?? false))
        {
            throw new DomainException(
                "La escuela aún no ha subido el comprobante de pago.",
                400,
                "payment_proof_required");
        }

        var wasActive = profile.IsCommerciallyActive(_clock.UtcNow);
        var force = request?.ForceWithoutProof ?? false;
        if (force)
        {
            profile.AdminOverrideMembership(
                plan,
                SchoolSubscriptionStatus.Active,
                null,
                clearRejection: true,
                _clock.UtcNow);
        }
        else
        {
            profile.ActivateOrRenew(plan, _clock.UtcNow);
        }

        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                wasActive ? MembershipEventTypes.Renewed : MembershipEventTypes.Activated,
                plan.Code,
                plan.PriceCop,
                adminUserId,
                force
                    ? "Activación forzada por administrador (sin comprobante)"
                    : "Pago verificado por administrador",
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        await _notifications.NotifyUserAsync(
            schoolUserId,
            wasActive ? "Membresía renovada" : "Membresía activada",
            wasActive
                ? $"Tu plan {plan.LabelEs} fue renovado. Ya puedes seguir gestionando usuarios."
                : $"Tu plan {plan.LabelEs} fue activado. Ya puedes gestionar docentes y estudiantes.",
            NotificationTypes.Membership,
            null,
            NotificationTypes.Membership,
            schoolUserId,
            ct);

        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> AdminRejectAsync(
        int schoolUserId,
        int adminUserId,
        RejectSchoolMembershipRequest? request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        var reason = string.IsNullOrWhiteSpace(request?.Note)
            ? "Pago no verificado"
            : request!.Note!;
        var planCode = profile.EffectiveRequestedPlanCode;
        var plan = SchoolPlans.Find(planCode);
        var keptActive = profile.IsCommerciallyActive(_clock.UtcNow);

        profile.RejectRequest(reason, _clock.UtcNow);
        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                MembershipEventTypes.Rejected,
                planCode,
                plan?.PriceCop ?? profile.PlanPriceCop,
                adminUserId,
                reason,
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        await _notifications.NotifyUserAsync(
            schoolUserId,
            "Solicitud de membresía rechazada",
            keptActive
                ? $"Tu solicitud de cambio/renovación fue rechazada: {reason}. Tu membresía actual sigue activa."
                : $"Tu solicitud fue rechazada: {reason}. Puedes corregir el pago y volver a solicitar.",
            NotificationTypes.Membership,
            null,
            NotificationTypes.Membership,
            schoolUserId,
            ct);

        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> CancelRequestAsync(
        int schoolUserId,
        int? actorUserId,
        CancelSchoolMembershipRequest? request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        var planCode = profile.EffectiveRequestedPlanCode;
        var plan = SchoolPlans.Find(planCode);
        var note = string.IsNullOrWhiteSpace(request?.Note) ? null : request!.Note;

        profile.CancelRequest(note, _clock.UtcNow);
        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                MembershipEventTypes.Cancelled,
                planCode,
                plan?.PriceCop ?? profile.PlanPriceCop,
                actorUserId,
                note,
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);
        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> AdminSuspendAsync(
        int schoolUserId,
        int adminUserId,
        SuspendSchoolMembershipRequest? request,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        var reason = string.IsNullOrWhiteSpace(request?.Note)
            ? "Suspensión administrativa"
            : request!.Note!;

        profile.Suspend(reason, _clock.UtcNow);
        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                MembershipEventTypes.Suspended,
                profile.PlanCode,
                profile.PlanPriceCop,
                adminUserId,
                reason,
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        await _notifications.NotifyUserAsync(
            schoolUserId,
            "Membresía suspendida",
            $"Tu membresía fue suspendida: {reason}",
            NotificationTypes.Membership,
            null,
            NotificationTypes.Membership,
            schoolUserId,
            ct);

        return await _profileQuery.MapAsync(user, profile, ct);
    }

    public async Task<SchoolProfileDto> AdminUnsuspendAsync(
        int schoolUserId,
        int adminUserId,
        CancellationToken ct)
    {
        var (user, profile) = await LoadSchoolAsync(schoolUserId, ct);
        profile.Unsuspend(_clock.UtcNow);
        await _events.AddAsync(
            MembershipEvent.Create(
                schoolUserId,
                MembershipEventTypes.Unsuspended,
                profile.PlanCode,
                profile.PlanPriceCop,
                adminUserId,
                null,
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        await _notifications.NotifyUserAsync(
            schoolUserId,
            "Membresía reactivada",
            profile.IsCommerciallyActive(_clock.UtcNow)
                ? "La suspensión fue levantada. Ya puedes operar de nuevo."
                : "La suspensión fue levantada, pero tu plan está vencido. Solicita renovación.",
            NotificationTypes.Membership,
            null,
            NotificationTypes.Membership,
            schoolUserId,
            ct);

        return await _profileQuery.MapAsync(user, profile, ct);
    }

    private async Task<(User User, SchoolProfile Profile)> LoadSchoolAsync(
        int userId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        if (Roles.Normalize(user.Role) != Roles.School)
        {
            throw new DomainException(
                "La cuenta no es una escuela.",
                400,
                "not_a_school");
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
