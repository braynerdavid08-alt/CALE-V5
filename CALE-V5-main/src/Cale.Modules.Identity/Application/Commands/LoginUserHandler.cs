using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class LoginUserHandler
{
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;
    private readonly ILogger<LoginUserHandler> _logger;

    public LoginUserHandler(
        IUserStore users,
        IPasswordHasher hasher,
        ITokenService tokens,
        IClock clock,
        ILogger<LoginUserHandler> logger)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthResponse> HandleAsync(
        LoginRequest request,
        CancellationToken ct)
    {
        var email = EmailAddress.Normalize(request.Email);
        var user = await _users.FindByEmailAsync(email, ct);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning(
                "Login failed invalid_credentials email={Email}",
                email);
            throw new UnauthorizedException(
                "Invalid credentials.",
                "invalid_credentials");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning(
                "Login failed user_inactive email={Email} userId={UserId}",
                email,
                user.Id);
            throw new ForbiddenException(
                "User is inactive.",
                "user_inactive");
        }

        if (_hasher.NeedsRehash(user.PasswordHash))
        {
            user.ChangePassword(_hasher.Hash(request.Password));
        }

        user.RecordLogin(_clock.UtcNow);
        await _users.SaveChangesAsync(ct);

        var role = Roles.Normalize(user.Role);
        var token = _tokens.Create(user.Id, user.Email, user.Name, role);

        _logger.LogInformation(
            "Login succeeded userId={UserId} email={Email} role={Role}",
            user.Id,
            email,
            role);

        return new AuthResponse(
            token,
            user.Id,
            user.Name,
            user.Email,
            role,
            user.MustChangePassword);
    }
}
