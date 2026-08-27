using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Services;
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

        if (!user.EmailConfirmed)
        {
            _logger.LogWarning(
                "Login failed email_not_confirmed email={Email} userId={UserId}",
                email,
                user.Id);
            throw new ForbiddenException(
                "Confirm your email before signing in.",
                "email_not_confirmed");
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

public sealed class ConfirmEmailHandler
{
    private readonly IUserStore _users;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;
    private readonly EmailConfirmationService _emailConfirmation;

    public ConfirmEmailHandler(
        IUserStore users,
        ITokenService tokens,
        IClock clock,
        EmailConfirmationService emailConfirmation)
    {
        _users = users;
        _tokens = tokens;
        _clock = clock;
        _emailConfirmation = emailConfirmation;
    }

    public async Task<AuthResponse> HandleAsync(
        ConfirmEmailRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new DomainException(
                "Confirmation code is required.",
                400,
                "invalid_confirmation_code");
        }

        var email = EmailAddress.Normalize(request.Email);
        await _emailConfirmation.ConfirmAsync(email, request.Code, ct);

        var user = await _users.FindByEmailAsync(email, ct)
            ?? throw new DomainException("User not found.", 404, "user_not_found");

        if (!user.IsActive)
        {
            throw new ForbiddenException("User is inactive.", "user_inactive");
        }

        user.RecordLogin(_clock.UtcNow);
        await _users.SaveChangesAsync(ct);

        var role = Roles.Normalize(user.Role);
        var token = _tokens.Create(user.Id, user.Email, user.Name, role);

        return new AuthResponse(
            token,
            user.Id,
            user.Name,
            user.Email,
            role,
            user.MustChangePassword);
    }
}

public sealed class ResendConfirmationHandler
{
    private readonly EmailConfirmationService _emailConfirmation;

    public ResendConfirmationHandler(EmailConfirmationService emailConfirmation) =>
        _emailConfirmation = emailConfirmation;

    public async Task<PendingEmailConfirmationResponse> HandleAsync(
        ResendConfirmationRequest request,
        CancellationToken ct)
    {
        var email = EmailAddress.Normalize(request.Email);
        await _emailConfirmation.ResendAsync(email, ct);
        return new PendingEmailConfirmationResponse(
            email,
            "Si la cuenta existe y aún no está confirmada, enviamos un nuevo código.");
    }
}
