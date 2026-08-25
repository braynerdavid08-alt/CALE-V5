using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class CreateTeacherHandler
{
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public CreateTeacherHandler(
        IUserStore users,
        IPasswordHasher hasher,
        IClock clock)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<UserListItemDto> HandleAsync(
        CreateTeacherRequest request,
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

        var user = User.CreateTeacher(
            request.Name,
            email,
            _hasher.Hash(request.Password),
            _clock.UtcNow);

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            Roles.Teacher,
            user.IsActive,
            user.CreatedAt);
    }

    private static void Validate(CreateTeacherRequest request)
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
