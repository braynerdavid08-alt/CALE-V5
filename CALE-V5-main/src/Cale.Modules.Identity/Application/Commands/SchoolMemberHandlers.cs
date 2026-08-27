using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

internal static class SchoolSeatGuard
{
    public static async Task EnsureCanAddAsync(
        IUserStore users,
        ISchoolProfileStore profiles,
        IClock clock,
        int schoolId,
        string role,
        CancellationToken ct)
    {
        var profile = await profiles.GetTrackedByUserIdAsync(schoolId, ct);
        if (profile is null)
        {
            var schoolUser = await users.GetByIdAsync(schoolId, ct)
                ?? throw new NotFoundException("Escuela no encontrada.", "user_not_found");
            var defaultPlan = SchoolPlans.Find(SchoolPlans.Monthly)!;
            profile = SchoolProfile.CreateDraft(
                schoolUser.Id,
                schoolUser.Name,
                schoolUser.Email,
                defaultPlan,
                clock.UtcNow);
            await profiles.AddAsync(profile, ct);
            await profiles.SaveChangesAsync(ct);
        }

        profile.RefreshStatus(clock.UtcNow);
        if (!profile.IsCommerciallyActive(clock.UtcNow))
        {
            throw new DomainException(
                "Tu membresía no está activa. Solicita un plan, sube el comprobante y espera la verificación del administrador.",
                400,
                "membership_inactive");
        }

        var plan = SchoolPlans.Find(profile.PlanCode)
            ?? throw new DomainException("Plan de escuela inválido.", 400, "invalid_plan");

        var used = await users.CountBySchoolAndRoleAsync(schoolId, role, ct);
        var max = role == Roles.Teacher
            ? profile.EffectiveMaxTeachers(plan)
            : profile.EffectiveMaxStudents(plan);
        if (used >= max)
        {
            throw new DomainException(
                role == Roles.Teacher
                    ? $"Límite de instructores alcanzado ({max})."
                    : $"Límite de estudiantes alcanzado ({max}).",
                400,
                "seat_limit_reached");
        }
    }

    public static string ParseMemberRole(string role) => role switch
    {
        "Teacher" or "Profesor" or "Instructor" => Roles.Teacher,
        "Student" or "Estudiante" or "Alumno" => Roles.Student,
        _ => throw new DomainException(
            "Solo puedes agregar instructores o estudiantes.",
            400,
            "invalid_role")
    };

    public static string RoleLabelEs(string role) =>
        role == Roles.Teacher ? "Instructor" : "Estudiante";
}

public sealed class CreateSchoolMemberHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IMembershipEventStore _events;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public CreateSchoolMemberHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IMembershipEventStore events,
        IPasswordHasher hasher,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _events = events;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<UserListItemDto> HandleAsync(
        int schoolId,
        CreateSchoolMemberRequest request,
        CancellationToken ct)
    {
        Validate(request);
        var role = SchoolSeatGuard.ParseMemberRole(request.Role);
        await SchoolSeatGuard.EnsureCanAddAsync(
            _users, _profiles, _clock, schoolId, role, ct);

        var email = EmailAddress.Normalize(request.Email);
        if (await _users.ExistsByEmailAsync(email, ct))
        {
            throw new ConflictException(
                "Ese correo ya está registrado. Usa «Vincular cuenta existente».",
                "email_taken");
        }

        var user = role == Roles.Teacher
            ? User.CreateTeacher(
                request.Name,
                email,
                _hasher.Hash(request.Password),
                _clock.UtcNow,
                schoolId)
            : User.RegisterStudent(
                request.Name,
                email,
                _hasher.Hash(request.Password),
                _clock.UtcNow,
                schoolId);

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        await _events.AddAsync(
            MembershipEvent.Create(
                schoolId,
                MembershipEventTypes.MemberCreated,
                null,
                null,
                schoolId,
                $"Alta {SchoolSeatGuard.RoleLabelEs(role)}: {user.Name} <{user.Email}> (#{user.Id})",
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        return Map(user, role);
    }

    private static void Validate(CreateSchoolMemberRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("El nombre es obligatorio.", 400, "invalid_name");
        }

        if (string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length < 8)
        {
            throw new DomainException(
                "La contraseña debe tener al menos 8 caracteres.",
                400,
                "weak_password");
        }
    }

    private static UserListItemDto Map(User user, string role) =>
        new(user.Id, user.Name, user.Email, role, user.IsActive, user.CreatedAt, user.LastLoginAt);
}

public sealed class AttachSchoolMemberHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IMembershipEventStore _events;
    private readonly IClock _clock;

    public AttachSchoolMemberHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IMembershipEventStore events,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _events = events;
        _clock = clock;
    }

    public async Task<UserListItemDto> HandleAsync(
        int schoolId,
        AttachSchoolMemberRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new DomainException("El correo es obligatorio.", 400, "invalid_email");
        }

        var expectedRole = SchoolSeatGuard.ParseMemberRole(request.Role);
        var email = EmailAddress.Normalize(request.Email);
        var user = await _users.FindByEmailAsync(email, ct)
            ?? throw new NotFoundException(
                "No hay ninguna cuenta con ese correo.",
                "user_not_found");

        var role = Roles.Normalize(user.Role);
        if (role is not (Roles.Teacher or Roles.Student))
        {
            throw new DomainException(
                "Solo se pueden vincular cuentas de instructor o estudiante.",
                400,
                "invalid_role");
        }

        if (role != expectedRole)
        {
            throw new DomainException(
                role == Roles.Teacher
                    ? "Esa cuenta es de instructor. Selecciona el tipo Instructor."
                    : "Esa cuenta es de estudiante. Selecciona el tipo Estudiante.",
                400,
                "role_mismatch");
        }

        if (user.SchoolId == schoolId)
        {
            throw new ConflictException(
                "Esa cuenta ya pertenece a tu escuela.",
                "already_member");
        }

        if (user.SchoolId is not null)
        {
            throw new ConflictException(
                "Esa cuenta ya está vinculada a otra escuela.",
                "already_in_other_school");
        }

        if (!user.IsActive)
        {
            throw new DomainException(
                "Esa cuenta está desactivada y no se puede vincular.",
                400,
                "user_inactive");
        }

        await SchoolSeatGuard.EnsureCanAddAsync(
            _users, _profiles, _clock, schoolId, role, ct);

        user.AssignSchool(schoolId);
        await _users.SaveChangesAsync(ct);

        await _events.AddAsync(
            MembershipEvent.Create(
                schoolId,
                MembershipEventTypes.MemberAttached,
                null,
                null,
                schoolId,
                $"Vinculación {SchoolSeatGuard.RoleLabelEs(role)}: {user.Name} <{user.Email}> (#{user.Id})",
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            role,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt);
    }
}

public sealed class UpdateSchoolMemberHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IMembershipEventStore _events;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public UpdateSchoolMemberHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IMembershipEventStore events,
        IPasswordHasher hasher,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _events = events;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<UserListItemDto> HandleAsync(
        int schoolId,
        int memberId,
        UpdateSchoolMemberRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("El nombre es obligatorio.", 400, "invalid_name");
        }

        var user = await OwnedMemberAsync(schoolId, memberId, ct);
        var email = EmailAddress.Normalize(request.Email);
        if (await _users.ExistsByEmailAsync(email, memberId, ct))
        {
            throw new ConflictException(
                "Ese correo ya está registrado.",
                "email_taken");
        }

        var previous = $"{user.Name} <{user.Email}>";
        user.UpdateProfile(request.Name, email);
        var passwordChanged = false;
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (request.NewPassword.Length < 8)
            {
                throw new DomainException(
                    "La contraseña debe tener al menos 8 caracteres.",
                    400,
                    "weak_password");
            }

            user.ChangePassword(_hasher.Hash(request.NewPassword));
            passwordChanged = true;
        }

        await _users.SaveChangesAsync(ct);

        var note = passwordChanged
            ? $"Edición {SchoolSeatGuard.RoleLabelEs(Roles.Normalize(user.Role))}: {previous} → {user.Name} <{user.Email}> (#{user.Id}); contraseña restablecida"
            : $"Edición {SchoolSeatGuard.RoleLabelEs(Roles.Normalize(user.Role))}: {previous} → {user.Name} <{user.Email}> (#{user.Id})";

        await _events.AddAsync(
            MembershipEvent.Create(
                schoolId,
                MembershipEventTypes.MemberUpdated,
                null,
                null,
                schoolId,
                note,
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        return Map(user);
    }

    public Task<UserListItemDto> SetActiveAsync(
        int schoolId,
        int memberId,
        bool isActive,
        CancellationToken ct) =>
        throw new ForbiddenException(
            "Solo el administrador puede activar o desactivar cuentas.",
            "admin_only_activation");

    public Task UnlinkAsync(
        int schoolId,
        int memberId,
        CancellationToken ct) =>
        throw new ForbiddenException(
            "Solo el administrador puede quitar o eliminar miembros. La escuela puede crear y editar nombre/correo.",
            "admin_only_unlink");

    private async Task<User> OwnedMemberAsync(
        int schoolId,
        int memberId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(memberId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");

        if (user.SchoolId != schoolId
            || Roles.Normalize(user.Role) is not (Roles.Teacher or Roles.Student))
        {
            throw new ForbiddenException(
                "Este usuario no es miembro de tu escuela.",
                "not_school_member");
        }

        return user;
    }

    private static UserListItemDto Map(User user) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            Roles.Normalize(user.Role),
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt);
}
