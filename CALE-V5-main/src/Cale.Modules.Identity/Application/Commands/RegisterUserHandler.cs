using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class RegisterUserHandler
{
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;

    public RegisterUserHandler(
        IUserStore users,
        IPasswordHasher hasher,
        ITokenService tokens,
        IClock clock)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<AuthResponse> HandleAsync(
        RegisterRequest request,
        CancellationToken ct)
    {
        Validate(request);
        var email = EmailAddress.Normalize(request.Email);

        if (await _users.ExistsByEmailAsync(email, ct))
        {
            throw new ConflictException(
                "Email already registered.",
                "email_taken");
        }

        var user = User.RegisterStudent(
            request.Name,
            email,
            _hasher.Hash(request.Password),
            _clock.UtcNow);

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        var token = _tokens.Create(
            user.Id,
            user.Email,
            user.Name,
            Roles.Student);

        return new AuthResponse(
            token,
            user.Id,
            user.Name,
            user.Email,
            Roles.Student);
    }

    private static void Validate(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Name is required.", 400, "invalid_name");
        }

        if (string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length < 8)
        {
            throw new DomainException(
                "Password must have at least 8 characters.",
                400,
                "weak_password");
        }
    }
}
