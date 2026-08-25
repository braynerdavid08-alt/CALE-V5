using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class LoginUserHandler
{
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public LoginUserHandler(
        IUserStore users,
        IPasswordHasher hasher,
        ITokenService tokens)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<AuthResponse> HandleAsync(
        LoginRequest request,
        CancellationToken ct)
    {
        var email = EmailAddress.Normalize(request.Email);
        var user = await _users.FindByEmailAsync(email, ct);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException(
                "Invalid credentials.",
                "invalid_credentials");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException(
                "User is inactive.",
                "user_inactive");
        }

        if (_hasher.NeedsRehash(user.PasswordHash))
        {
            user.ChangePassword(_hasher.Hash(request.Password));
            await _users.SaveChangesAsync(ct);
        }

        var role = Roles.Normalize(user.Role);
        var token = _tokens.Create(user.Id, user.Email, user.Name, role);
        return new AuthResponse(token, user.Id, user.Name, user.Email, role);
    }
}
