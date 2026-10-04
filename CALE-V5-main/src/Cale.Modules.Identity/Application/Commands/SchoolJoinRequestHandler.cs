using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

/// <summary>
/// Links teacher/student accounts to a school only with both sides' consent: the member requests and
/// the school accepts, or the school invites and the member accepts. Schools never create accounts.
/// </summary>
public sealed class SchoolJoinRequestHandler
{
    private const int MaxPendingRequestsPerMember = 5;

    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly ISchoolJoinRequestStore _requests;
    private readonly IMembershipEventStore _events;
    private readonly INotificationPublisher _notifications;
    private readonly ISchoolStudentEnrollmentBootstrap _enrollmentBootstrap;
    private readonly IClock _clock;

    public SchoolJoinRequestHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        ISchoolJoinRequestStore requests,
        IMembershipEventStore events,
        INotificationPublisher notifications,
        ISchoolStudentEnrollmentBootstrap enrollmentBootstrap,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _requests = requests;
        _events = events;
        _notifications = notifications;
        _enrollmentBootstrap = enrollmentBootstrap;
        _clock = clock;
    }

    // ── Member side (teacher or student) ────────────────────────────────────────────────────

    public async Task<SchoolJoinRequestDto> RequestAsync(
        int memberUserId,
        RequestSchoolJoinRequest request,
        CancellationToken ct)
    {
        var member = await GetEligibleMemberAsync(memberUserId, ct);

        var query = (request.SchoolQuery ?? "").Trim();
        if (query.Length < 3)
        {
            throw new DomainException(
                "Indica el NIT o el correo de la escuela.",
                400,
                "invalid_school_query");
        }

        var school = await ResolveSchoolAsync(query, ct)
            ?? throw new NotFoundException(
                "No encontramos una escuela con ese NIT o correo.",
                "school_not_found");

        var existing = await _requests.FindPendingAsync(memberUserId, school.UserId, ct);
        if (existing is not null)
        {
            if (existing.IsInvite)
            {
                await LinkAsync(existing, member, memberUserId, ct);
                return await MapAsync(existing, ct);
            }

            throw new ConflictException(
                "Ya tienes una solicitud pendiente con esa escuela.",
                "join_request_pending");
        }

        var pending = await _requests.ListPendingByMemberAsync(memberUserId, ct);
        if (pending.Count(x => !x.IsInvite) >= MaxPendingRequestsPerMember)
        {
            throw new DomainException(
                $"Tienes {MaxPendingRequestsPerMember} solicitudes pendientes. Cancela alguna antes de enviar otra.",
                400,
                "too_many_pending_requests");
        }

        var join = SchoolJoinRequest.Create(
            memberUserId,
            school.UserId,
            request.Message,
            _clock.UtcNow,
            SchoolJoinDirections.Request);
        await _requests.AddAsync(join, ct);
        await _requests.SaveChangesAsync(ct);

        var roleLabel = SchoolSeatGuard.RoleLabelEs(Roles.Normalize(member.Role)).ToLowerInvariant();
        await _notifications.NotifyUsersAsync(
            [school.UserId],
            new NotificationDraft(
                $"Solicitud de {roleLabel}",
                $"{member.Name} ({member.Email}) quiere unirse a tu escuela como {roleLabel}.",
                NotificationTypes.Membership,
                RelatedEntity: "school_join_request",
                RelatedId: join.Id,
                Link: "/school/users",
                Priority: NotificationPriorities.High,
                DedupeKey: $"school-join-{join.Id}"),
            ct);

        return await MapAsync(join, ct);
    }

    public async Task<IReadOnlyList<SchoolJoinRequestDto>> ListMineAsync(
        int memberUserId,
        CancellationToken ct)
    {
        var list = await _requests.ListByMemberAsync(memberUserId, ct);
        var result = new List<SchoolJoinRequestDto>(list.Count);
        foreach (var item in list)
        {
            result.Add(await MapAsync(item, ct));
        }

        return result;
    }

    public async Task CancelAsync(int memberUserId, int requestId, CancellationToken ct)
    {
        var join = await GetPendingAsync(requestId, ct);
        if (join.MemberUserId != memberUserId || join.IsInvite)
        {
            throw new ForbiddenException("No puedes cancelar esta solicitud.", "not_your_request");
        }

        join.Cancel(memberUserId, _clock.UtcNow);
        await _requests.SaveChangesAsync(ct);
    }

    public async Task<SchoolJoinRequestDto> AcceptInviteAsync(
        int memberUserId,
        int requestId,
        CancellationToken ct)
    {
        var join = await GetPendingAsync(requestId, ct);
        if (join.MemberUserId != memberUserId || !join.IsInvite)
        {
            throw new ForbiddenException("Esa invitación no es para ti.", "not_your_invitation");
        }

        var member = await GetEligibleMemberAsync(memberUserId, ct);
        await LinkAsync(join, member, memberUserId, ct);
        return await MapAsync(join, ct);
    }

    public async Task<SchoolJoinRequestDto> RejectInviteAsync(
        int memberUserId,
        int requestId,
        CancellationToken ct)
    {
        var join = await GetPendingAsync(requestId, ct);
        if (join.MemberUserId != memberUserId || !join.IsInvite)
        {
            throw new ForbiddenException("Esa invitación no es para ti.", "not_your_invitation");
        }

        join.Reject(memberUserId, null, _clock.UtcNow);
        await _requests.SaveChangesAsync(ct);

        var member = await _users.GetByIdAsync(memberUserId, ct);
        await _notifications.NotifyUsersAsync(
            [join.SchoolUserId],
            new NotificationDraft(
                "Invitación rechazada",
                $"{member?.Name ?? "La persona"} rechazó la invitación a tu escuela.",
                NotificationTypes.Membership,
                RelatedEntity: "school_join_request",
                RelatedId: join.Id,
                Link: "/school/users",
                Priority: NotificationPriorities.Normal),
            ct);

        return await MapAsync(join, ct);
    }

    // ── School side ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SchoolJoinRequestDto>> ListPendingForSchoolAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var list = await _requests.ListPendingBySchoolAsync(schoolUserId, ct);
        var result = new List<SchoolJoinRequestDto>(list.Count);
        foreach (var item in list)
        {
            result.Add(await MapAsync(item, ct));
        }

        return result;
    }

    public async Task<SchoolInviteResultDto> InviteAsync(
        int schoolUserId,
        InviteSchoolMemberRequest request,
        CancellationToken ct)
    {
        var role = SchoolSeatGuard.ParseMemberRole(request.Role ?? "");
        var rawEmail = (request.Email ?? "").Trim();
        if (rawEmail.Length == 0 || !rawEmail.Contains('@'))
        {
            throw new DomainException("Indica un correo válido.", 400, "invalid_email");
        }

        await SchoolSeatGuard.EnsureCanAddAsync(_users, _profiles, _clock, schoolUserId, role, ct);

        var email = EmailAddress.Normalize(rawEmail);
        var member = await _users.FindByEmailAsync(email, ct);
        if (member is not null && member.SchoolId == schoolUserId)
        {
            throw new ConflictException("Esa cuenta ya pertenece a tu escuela.", "already_member");
        }

        var roleLabel = SchoolSeatGuard.RoleLabelEs(role).ToLowerInvariant();
        if (member is null
            || Roles.Normalize(member.Role) != role
            || member.SchoolId is not null
            || !member.IsActive)
        {
            throw new NotFoundException(
                $"No hay una cuenta de {roleLabel} disponible con ese correo. "
                + "La persona debe registrarse primero en la plataforma y no pertenecer a otra escuela.",
                "invite_target_unavailable");
        }

        var existing = await _requests.FindPendingAsync(member.Id, schoolUserId, ct);
        if (existing is not null)
        {
            if (!existing.IsInvite)
            {
                await LinkAsync(existing, member, schoolUserId, ct);
                return new SchoolInviteResultDto(
                    $"{member.Name} ya había solicitado unirse: quedó vinculado a tu escuela.");
            }

            throw new ConflictException(
                "Ya enviaste una invitación a esa cuenta.",
                "invitation_pending");
        }

        var join = SchoolJoinRequest.Create(
            member.Id,
            schoolUserId,
            request.Message,
            _clock.UtcNow,
            SchoolJoinDirections.Invite);
        await _requests.AddAsync(join, ct);
        await _requests.SaveChangesAsync(ct);

        var school = await _profiles.GetByUserIdAsync(schoolUserId, ct);
        await _notifications.NotifyUsersAsync(
            [member.Id],
            new NotificationDraft(
                "Invitación de una escuela",
                $"{school?.LegalName ?? "Una escuela"} te invitó a unirte como {roleLabel}. Acepta o rechaza desde tu perfil.",
                NotificationTypes.Membership,
                RelatedEntity: "school_join_request",
                RelatedId: join.Id,
                Link: "/profile",
                Priority: NotificationPriorities.High,
                DedupeKey: $"school-invite-{join.Id}"),
            ct);

        return new SchoolInviteResultDto(
            $"Invitación enviada. {member.Name} quedará vinculado cuando la acepte.");
    }

    public async Task<SchoolJoinRequestDto> AcceptAsync(
        int schoolUserId,
        int requestId,
        CancellationToken ct)
    {
        var join = await GetPendingAsync(requestId, ct);
        if (join.SchoolUserId != schoolUserId || join.IsInvite)
        {
            throw new ForbiddenException("Esa solicitud no es de tu escuela.", "not_your_request");
        }

        var member = await _users.GetByIdAsync(join.MemberUserId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");
        await LinkAsync(join, member, schoolUserId, ct);
        return await MapAsync(join, ct);
    }

    public async Task<SchoolJoinRequestDto> RejectAsync(
        int schoolUserId,
        int requestId,
        RejectSchoolJoinRequest? body,
        CancellationToken ct)
    {
        var join = await GetPendingAsync(requestId, ct);
        if (join.SchoolUserId != schoolUserId || join.IsInvite)
        {
            throw new ForbiddenException("Esa solicitud no es de tu escuela.", "not_your_request");
        }

        join.Reject(schoolUserId, body?.Reason, _clock.UtcNow);
        await _requests.SaveChangesAsync(ct);

        await _notifications.NotifyUsersAsync(
            [join.MemberUserId],
            new NotificationDraft(
                "Solicitud rechazada",
                string.IsNullOrWhiteSpace(body?.Reason)
                    ? "La escuela rechazó tu solicitud de unión."
                    : $"La escuela rechazó tu solicitud: {body!.Reason.Trim()}",
                NotificationTypes.Membership,
                RelatedEntity: "school_join_request",
                RelatedId: join.Id,
                Link: "/profile",
                Priority: NotificationPriorities.Normal),
            ct);

        return await MapAsync(join, ct);
    }

    public async Task CancelInviteAsync(int schoolUserId, int requestId, CancellationToken ct)
    {
        var join = await GetPendingAsync(requestId, ct);
        if (join.SchoolUserId != schoolUserId || !join.IsInvite)
        {
            throw new ForbiddenException("Esa invitación no es de tu escuela.", "not_your_invitation");
        }

        join.Cancel(schoolUserId, _clock.UtcNow);
        await _requests.SaveChangesAsync(ct);
    }

    // ── Shared ──────────────────────────────────────────────────────────────────────────────

    private async Task LinkAsync(SchoolJoinRequest join, User member, int decidedByUserId, CancellationToken ct)
    {
        var schoolUserId = join.SchoolUserId;
        var role = Roles.Normalize(member.Role);
        if (role is not (Roles.Teacher or Roles.Student))
        {
            throw new DomainException("La cuenta ya no es de instructor ni de estudiante.", 400, "invalid_role");
        }

        if (member.SchoolId is not null && member.SchoolId != schoolUserId)
        {
            throw new ConflictException(
                "Esa cuenta ya está vinculada a otra escuela.",
                "already_in_other_school");
        }

        if (!member.IsActive)
        {
            throw new DomainException("Esa cuenta está desactivada.", 400, "user_inactive");
        }

        if (member.SchoolId is null)
        {
            await SchoolSeatGuard.EnsureCanAddAsync(_users, _profiles, _clock, schoolUserId, role, ct);
            member.AssignSchool(schoolUserId);
            await _users.SaveChangesAsync(ct);

            if (role == Roles.Student)
            {
                await _enrollmentBootstrap.EnsurePendingAsync(schoolUserId, member.Id, ct);
            }

            await _events.AddAsync(
                MembershipEvent.Create(
                    schoolUserId,
                    MembershipEventTypes.MemberAttached,
                    null,
                    null,
                    schoolUserId,
                    join.IsInvite
                        ? $"{SchoolSeatGuard.RoleLabelEs(role)} {member.Name} <{member.Email}> (#{member.Id}) aceptó la invitación"
                        : $"{SchoolSeatGuard.RoleLabelEs(role)} {member.Name} <{member.Email}> (#{member.Id}) aceptado por solicitud",
                    _clock.UtcNow),
                ct);
            await _profiles.SaveChangesAsync(ct);
        }

        var now = _clock.UtcNow;
        join.Accept(decidedByUserId, now);
        foreach (var other in await _requests.ListPendingByMemberAsync(member.Id, ct))
        {
            if (other.Id != join.Id)
            {
                other.Cancel(decidedByUserId, now);
            }
        }

        await _requests.SaveChangesAsync(ct);

        if (join.IsInvite)
        {
            await _notifications.NotifyUsersAsync(
                [schoolUserId],
                new NotificationDraft(
                    "Invitación aceptada",
                    $"{member.Name} aceptó la invitación y ya forma parte de tu escuela.",
                    NotificationTypes.Membership,
                    RelatedEntity: "school_join_request",
                    RelatedId: join.Id,
                    Link: "/school/users",
                    Priority: NotificationPriorities.Normal),
                ct);
        }
        else
        {
            await _notifications.NotifyUsersAsync(
                [member.Id],
                new NotificationDraft(
                    "Solicitud aceptada",
                    "La escuela aceptó tu solicitud. Ya formas parte de ella.",
                    NotificationTypes.Membership,
                    RelatedEntity: "school_join_request",
                    RelatedId: join.Id,
                    Link: "/profile",
                    Priority: NotificationPriorities.Normal),
                ct);
        }
    }

    private async Task<User> GetEligibleMemberAsync(int memberUserId, CancellationToken ct)
    {
        var member = await _users.GetByIdAsync(memberUserId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        if (Roles.Normalize(member.Role) is not (Roles.Teacher or Roles.Student))
        {
            throw new DomainException(
                "Solo instructores y estudiantes pueden unirse a una escuela.",
                403,
                "member_only");
        }

        if (member.SchoolId is not null)
        {
            throw new ConflictException(
                "Ya estás vinculado a una escuela.",
                "already_in_school");
        }

        return member;
    }

    private async Task<SchoolJoinRequest> GetPendingAsync(int requestId, CancellationToken ct)
    {
        var join = await _requests.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException("Solicitud no encontrada.", "join_request_not_found");
        if (join.Status != SchoolJoinRequestStatuses.Pending)
        {
            throw new DomainException("La solicitud ya fue resuelta.", 400, "join_request_closed");
        }

        return join;
    }

    private async Task<SchoolProfile?> ResolveSchoolAsync(string query, CancellationToken ct)
    {
        if (query.Contains('@'))
        {
            var email = EmailAddress.Normalize(query);
            var byBilling = (await _profiles.ListAllAsync(ct))
                .FirstOrDefault(x => x.BillingEmail == email);
            if (byBilling is not null)
            {
                return byBilling;
            }

            var schoolUser = await _users.FindByEmailAsync(email, ct);
            if (schoolUser is not null && Roles.Normalize(schoolUser.Role) == Roles.School)
            {
                return await _profiles.GetByUserIdAsync(schoolUser.Id, ct);
            }

            return null;
        }

        var tax = NormalizeTaxId(query);
        if (tax.Length < 3)
        {
            return null;
        }

        var profiles = await _profiles.ListAllAsync(ct);
        return profiles.FirstOrDefault(p => NormalizeTaxId(p.TaxId) == tax);
    }

    private static string NormalizeTaxId(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private async Task<SchoolJoinRequestDto> MapAsync(SchoolJoinRequest join, CancellationToken ct)
    {
        var member = await _users.GetByIdAsync(join.MemberUserId, ct);
        var school = await _profiles.GetByUserIdAsync(join.SchoolUserId, ct);
        return new SchoolJoinRequestDto(
            join.Id,
            join.Direction,
            join.MemberUserId,
            member?.Name ?? "",
            member?.Email ?? "",
            member is null ? "" : Roles.Normalize(member.Role),
            join.SchoolUserId,
            school?.LegalName ?? "Escuela",
            school?.TaxId ?? "",
            join.Status,
            join.Message,
            join.RejectionReason,
            join.CreatedAt,
            join.DecidedAt);
    }
}
