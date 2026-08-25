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
        if (profile.SubscriptionStatus != SchoolSubscriptionStatus.Active
            || profile.DaysRemaining(clock.UtcNow) <= 0)
        {
            throw new DomainException(
                "Tu membresía no está activa. Activa o renueva un plan para gestionar usuarios.",
                400,
                "membership_inactive");
        }

        var plan = SchoolPlans.Find(profile.PlanCode)
            ?? throw new DomainException("Plan de escuela inválido.", 400, "invalid_plan");

        var used = await users.CountBySchoolAndRoleAsync(schoolId, role, ct);
        var max = role == Roles.Teacher ? plan.MaxTeachers : plan.MaxStudents;
        if (used >= max)
        {
            throw new DomainException(
                role == Roles.Teacher
                    ? $"Límite de docentes alcanzado ({max})."
                    : $"Límite de estudiantes alcanzado ({max}).",
                400,
                "seat_limit_reached");
        }
    }

    public static string ParseMemberRole(string role) => role switch
    {
        "Teacher" or "Profesor" => Roles.Teacher,
        "Student" or "Estudiante" or "Alumno" => Roles.Student,
        _ => throw new DomainException(
            "Solo puedes agregar docentes o estudiantes.",
            400,
            "invalid_role")
    };
}

public sealed class CreateSchoolMemberHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public CreateSchoolMemberHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IPasswordHasher hasher,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
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
        new(user.Id, user.Name, user.Email, role, user.IsActive, user.CreatedAt);
}

public sealed class AttachSchoolMemberHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IClock _clock;

    public AttachSchoolMemberHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
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
                "Solo se pueden vincular cuentas de docente o estudiante.",
                400,
                "invalid_role");
        }

        if (role != expectedRole)
        {
            throw new DomainException(
                role == Roles.Teacher
                    ? "Esa cuenta es de docente. Selecciona el tipo Docente."
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

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            role,
            user.IsActive,
            user.CreatedAt);
    }
}

public sealed class UpdateSchoolMemberHandler
{
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;

    public UpdateSchoolMemberHandler(IUserStore users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
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

        user.UpdateProfile(request.Name, email);
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
        }

        await _users.SaveChangesAsync(ct);
        return Map(user);
    }

    public async Task<UserListItemDto> SetActiveAsync(
        int schoolId,
        int memberId,
        bool isActive,
        CancellationToken ct)
    {
        var user = await OwnedMemberAsync(schoolId, memberId, ct);
        if (isActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await _users.SaveChangesAsync(ct);
        return Map(user);
    }

    public async Task UnlinkAsync(
        int schoolId,
        int memberId,
        CancellationToken ct)
    {
        var user = await OwnedMemberAsync(schoolId, memberId, ct);
        user.LeaveSchool();
        await _users.SaveChangesAsync(ct);
    }

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
            user.CreatedAt);
}
