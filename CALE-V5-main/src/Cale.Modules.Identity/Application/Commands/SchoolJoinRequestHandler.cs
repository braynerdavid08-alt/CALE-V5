using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class SchoolJoinRequestHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly ISchoolJoinRequestStore _requests;
    private readonly IMembershipEventStore _events;
    private readonly INotificationPublisher _notifications;
    private readonly IClock _clock;
    private readonly Cale.BuildingBlocks.Infrastructure.Persistence.CaleDbContext _db;

    public SchoolJoinRequestHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        ISchoolJoinRequestStore requests,
        IMembershipEventStore events,
        INotificationPublisher notifications,
        IClock clock,
        Cale.BuildingBlocks.Infrastructure.Persistence.CaleDbContext db)
    {
        _users = users;
        _profiles = profiles;
        _requests = requests;
        _events = events;
        _notifications = notifications;
        _clock = clock;
        _db = db;
    }

    public async Task<SchoolJoinRequestDto> RequestAsync(
        int teacherUserId,
        RequestSchoolJoinRequest request,
        CancellationToken ct)
    {
        var teacher = await _users.GetByIdAsync(teacherUserId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        if (Roles.Normalize(teacher.Role) != Roles.Teacher)
        {
            throw new DomainException(
                "Solo los instructores pueden solicitar unirse a una escuela.",
                403,
                "teacher_only");
        }

        if (teacher.SchoolId is not null)
        {
            throw new ConflictException(
                "Ya estás vinculado a una escuela.",
                "already_in_school");
        }

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

        var existing = await _requests.FindPendingAsync(teacherUserId, school.UserId, ct);
        if (existing is not null)
        {
            throw new ConflictException(
                "Ya tienes una solicitud pendiente con esa escuela.",
                "join_request_pending");
        }

        var join = SchoolJoinRequest.Create(
            teacherUserId,
            school.UserId,
            request.Message,
            _clock.UtcNow);
        await _requests.AddAsync(join, ct);
        await _requests.SaveChangesAsync(ct);

        await _notifications.NotifyUsersAsync(
            [school.UserId],
            new NotificationDraft(
                "Solicitud de instructor",
                $"{teacher.Name} ({teacher.Email}) quiere unirse a tu escuela.",
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
        int teacherUserId,
        CancellationToken ct)
    {
        var list = await _requests.ListByTeacherAsync(teacherUserId, ct);
        var result = new List<SchoolJoinRequestDto>(list.Count);
        foreach (var item in list)
        {
            result.Add(await MapAsync(item, ct));
        }

        return result;
    }

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

    public async Task<SchoolJoinRequestDto> AcceptAsync(
        int schoolUserId,
        int requestId,
        CancellationToken ct)
    {
        var join = await _requests.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException("Solicitud no encontrada.", "join_request_not_found");

        if (join.SchoolUserId != schoolUserId)
        {
            throw new ForbiddenException("Esa solicitud no es de tu escuela.", "not_your_request");
        }

        if (join.Status != SchoolJoinRequestStatuses.Pending)
        {
            throw new DomainException("La solicitud ya fue resuelta.", 400, "join_request_closed");
        }

        var teacher = await _users.GetByIdAsync(join.TeacherUserId, ct)
            ?? throw new NotFoundException("Instructor no encontrado.", "user_not_found");

        if (Roles.Normalize(teacher.Role) != Roles.Teacher)
        {
            throw new DomainException("La cuenta ya no es de instructor.", 400, "invalid_role");
        }

        if (teacher.SchoolId is not null && teacher.SchoolId != schoolUserId)
        {
            throw new ConflictException(
                "Ese instructor ya está vinculado a otra escuela.",
                "already_in_other_school");
        }

        if (teacher.SchoolId is null)
        {
            await SchoolSeatGuard.EnsureCanAddAsync(
                _users, _profiles, _clock, schoolUserId, Roles.Teacher, ct);
            teacher.AssignSchool(schoolUserId);
            await _users.SaveChangesAsync(ct);

            await _events.AddAsync(
                MembershipEvent.Create(
                    schoolUserId,
                    MembershipEventTypes.MemberAttached,
                    null,
                    null,
                    schoolUserId,
                    $"Instructor {teacher.Email} aceptado por solicitud",
                    _clock.UtcNow),
                ct);
            await _profiles.SaveChangesAsync(ct);
        }

        join.Accept(schoolUserId, _clock.UtcNow);
        await _requests.SaveChangesAsync(ct);

        await _notifications.NotifyUsersAsync(
            [teacher.Id],
            new NotificationDraft(
                "Solicitud aceptada",
                "La escuela aceptó tu solicitud. Ya formas parte de su equipo.",
                NotificationTypes.Membership,
                RelatedEntity: "school_join_request",
                RelatedId: join.Id,
                Link: "/profile",
                Priority: NotificationPriorities.Normal),
            ct);

        return await MapAsync(join, ct);
    }

    public async Task<SchoolJoinRequestDto> RejectAsync(
        int schoolUserId,
        int requestId,
        RejectSchoolJoinRequest? body,
        CancellationToken ct)
    {
        var join = await _requests.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException("Solicitud no encontrada.", "join_request_not_found");

        if (join.SchoolUserId != schoolUserId)
        {
            throw new ForbiddenException("Esa solicitud no es de tu escuela.", "not_your_request");
        }

        if (join.Status != SchoolJoinRequestStatuses.Pending)
        {
            throw new DomainException("La solicitud ya fue resuelta.", 400, "join_request_closed");
        }

        join.Reject(schoolUserId, body?.Reason, _clock.UtcNow);
        await _requests.SaveChangesAsync(ct);

        await _notifications.NotifyUsersAsync(
            [join.TeacherUserId],
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

    public async Task CancelAsync(int teacherUserId, int requestId, CancellationToken ct)
    {
        var join = await _requests.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException("Solicitud no encontrada.", "join_request_not_found");

        if (join.TeacherUserId != teacherUserId)
        {
            throw new ForbiddenException("No puedes cancelar esta solicitud.", "not_your_request");
        }

        if (join.Status != SchoolJoinRequestStatuses.Pending)
        {
            throw new DomainException("La solicitud ya fue resuelta.", 400, "join_request_closed");
        }

        join.Cancel(_clock.UtcNow);
        await _requests.SaveChangesAsync(ct);
    }

    private async Task<SchoolProfile?> ResolveSchoolAsync(string query, CancellationToken ct)
    {
        if (query.Contains('@'))
        {
            var email = EmailAddress.Normalize(query);
            var byBilling = await _db.Set<SchoolProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BillingEmail == email, ct);
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

        var profiles = await _db.Set<SchoolProfile>().AsNoTracking().ToListAsync(ct);
        return profiles.FirstOrDefault(p => NormalizeTaxId(p.TaxId) == tax);
    }

    private static string NormalizeTaxId(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private async Task<SchoolJoinRequestDto> MapAsync(SchoolJoinRequest join, CancellationToken ct)
    {
        var teacher = await _users.GetByIdAsync(join.TeacherUserId, ct);
        var school = await _profiles.GetByUserIdAsync(join.SchoolUserId, ct);
        return new SchoolJoinRequestDto(
            join.Id,
            join.TeacherUserId,
            teacher?.Name ?? "",
            teacher?.Email ?? "",
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
