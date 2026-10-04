using Cale.BuildingBlocks.Domain.Abstractions;
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
        if (!profile.CanOperateProduct(clock.UtcNow))
        {
            throw new DomainException(
                "Tu membresía no está activa. Solicita un plan, sube el comprobante y espera la verificación del administrador.",
                400,
                "membership_inactive");
        }

        if (!SchoolProfile.SeatLimitsEnforced)
        {
            return;
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
            "Solo puedes invitar instructores o estudiantes.",
            400,
            "invalid_role")
    };

    public static string RoleLabelEs(string role) =>
        role == Roles.Teacher ? "Instructor" : "Estudiante";
}

public sealed class UpdateSchoolMemberHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IMembershipEventStore _events;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IRefreshTokenStore _refreshTokens;

    public UpdateSchoolMemberHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IMembershipEventStore events,
        IPasswordHasher hasher,
        IClock clock,
        IRefreshTokenStore refreshTokens)
    {
        _users = users;
        _profiles = profiles;
        _events = events;
        _hasher = hasher;
        _clock = clock;
        _refreshTokens = refreshTokens;
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
        if (!string.IsNullOrWhiteSpace(request.Email)
            && !string.Equals(EmailAddress.Normalize(request.Email), user.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                "El correo de acceso no se puede cambiar.",
                400,
                "email_change_disabled");
        }

        var resetsPassword = !string.IsNullOrWhiteSpace(request.NewPassword);
        if (resetsPassword && !user.CredentialsManagedBy(schoolId))
        {
            throw new DomainException(
                "Esta cuenta pertenece a su dueño: no puedes cambiar su contraseña. Pídele que lo haga desde su perfil.",
                403,
                "credentials_not_managed");
        }

        var previous = user.Name;
        user.UpdateProfile(request.Name, user.Email);
        if (resetsPassword)
        {
            if (request.NewPassword!.Length < 8)
            {
                throw new DomainException(
                    "La contraseña debe tener al menos 8 caracteres.",
                    400,
                    "weak_password");
            }

            user.ChangePassword(_hasher.Hash(request.NewPassword));
        }

        await _users.SaveChangesAsync(ct);
        if (resetsPassword)
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
        }

        var label = SchoolSeatGuard.RoleLabelEs(Roles.Normalize(user.Role));
        var note = resetsPassword
            ? $"Edición {label}: {previous} → {user.Name} <{user.Email}> (#{user.Id}); contraseña restablecida"
            : $"Edición {label}: {previous} → {user.Name} <{user.Email}> (#{user.Id})";

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
            "Solo el administrador puede quitar o eliminar miembros.",
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
