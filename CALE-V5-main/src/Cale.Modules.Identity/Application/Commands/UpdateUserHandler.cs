using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class UpdateUserHandler
{
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;

    public UpdateUserHandler(IUserStore users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task<UserListItemDto> HandleAsync(
        int actorUserId,
        int targetUserId,
        UpdateUserRequest request,
        CancellationToken ct)
    {
        Validate(request);

        var user = await _users.GetByIdAsync(targetUserId, ct)
            ?? throw new NotFoundException("User not found.", "user_not_found");

        var email = EmailAddress.Normalize(request.Email);
        if (await _users.ExistsByEmailAsync(email, targetUserId, ct))
        {
            throw new ConflictException(
                "Email already registered.",
                "email_taken");
        }

        var role = ParseRole(request.Role);
        if (actorUserId == targetUserId && role != Roles.Admin)
        {
            throw new DomainException(
                "You cannot remove your own admin role.",
                400,
                "cannot_demote_self");
        }

        user.UpdateProfile(request.Name, email);
        user.ChangeRole(role);

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (request.NewPassword.Length < 8)
            {
                throw new DomainException(
                    "Password must have at least 8 characters.",
                    400,
                    "weak_password");
            }

            user.ChangePassword(_hasher.Hash(request.NewPassword));
        }

        await _users.SaveChangesAsync(ct);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            Roles.Normalize(user.Role),
            user.IsActive,
            user.CreatedAt);
    }

    private static string ParseRole(string role) => role switch
    {
        "Admin" or "Administrador" => Roles.Admin,
        "School" or "Escuela" => Roles.School,
        "Teacher" or "Profesor" => Roles.Teacher,
        "Student" or "Estudiante" or "Alumno" => Roles.Student,
        _ => throw new DomainException("Invalid role.", 400, "invalid_role")
    };

    private static void Validate(UpdateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Name is required.", 400, "invalid_name");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new DomainException("Email is required.", 400, "invalid_email");
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            throw new DomainException("Role is required.", 400, "invalid_role");
        }
    }
}
