using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class UpdateUserHandler
{
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly ILogger<UpdateUserHandler> _logger;

    public UpdateUserHandler(
        IUserStore users,
        IPasswordHasher hasher,
        IRefreshTokenStore refreshTokens,
        ILogger<UpdateUserHandler> logger)
    {
        _users = users;
        _hasher = hasher;
        _refreshTokens = refreshTokens;
        _logger = logger;
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

        var previousRole = Roles.Normalize(user.Role);
        var previousEmail = user.Email;
        user.UpdateProfile(request.Name, email);
        user.ChangeRole(role);

        var passwordChanged = false;
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
            passwordChanged = true;
        }

        await _users.SaveChangesAsync(ct);

        var roleChanged = previousRole != role;
        var emailChanged = !string.Equals(previousEmail, email, StringComparison.OrdinalIgnoreCase);
        if (roleChanged || passwordChanged || emailChanged)
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
        }

        _logger.LogWarning(
            "Audit: admin {ActorId} updated user {TargetId} (role {PreviousRole} -> {Role}, passwordReset={PasswordReset}, emailChanged={EmailChanged})",
            actorUserId,
            user.Id,
            previousRole,
            role,
            passwordChanged,
            emailChanged);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            Roles.Normalize(user.Role),
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt);
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
