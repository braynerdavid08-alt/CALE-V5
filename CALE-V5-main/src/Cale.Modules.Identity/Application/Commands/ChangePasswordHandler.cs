using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class ChangePasswordHandler
{
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;

    public ChangePasswordHandler(IUserStore users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task HandleAsync(
        int userId,
        ChangePasswordRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword)
            || request.NewPassword.Length < 8)
        {
            throw new DomainException(
                "Password must have at least 8 characters.",
                400,
                "weak_password");
        }

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found.", "user_not_found");

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedException(
                "Current password is incorrect.",
                "invalid_credentials");
        }

        user.ChangePassword(_hasher.Hash(request.NewPassword));
        await _users.SaveChangesAsync(ct);
    }
}
