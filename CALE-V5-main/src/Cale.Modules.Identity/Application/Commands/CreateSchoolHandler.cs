using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class CreateSchoolHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IMembershipEventStore _events;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public CreateSchoolHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IMembershipEventStore events,
        IPasswordHasher hasher,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _events = events;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<UserListItemDto> HandleAsync(
        CreateSchoolRequest request,
        int? actorUserId,
        CancellationToken ct)
    {
        Validate(request);
        var plan = SchoolPlans.Find(request.PlanCode)
            ?? SchoolPlans.Find(SchoolPlans.Monthly)!;

        var email = EmailAddress.Normalize(request.Email);
        var billingEmail = EmailAddress.Normalize(
            string.IsNullOrWhiteSpace(request.BillingEmail)
                ? request.Email
                : request.BillingEmail);

        if (await _users.ExistsByEmailAsync(email, ct))
        {
            throw new ConflictException(
                "Email already registered.",
                "email_taken");
        }

        var contactName = string.IsNullOrWhiteSpace(request.ContactName)
            ? request.LegalName.Trim()
            : request.ContactName.Trim();

        var user = User.RegisterSchool(
            contactName,
            email,
            _hasher.Hash(request.Password),
            _clock.UtcNow);
        user.MarkEmailConfirmed();

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        var profile = SchoolProfile.Create(
            user.Id,
            request.LegalName.Trim(),
            string.IsNullOrWhiteSpace(request.TaxId) ? "000000000-0" : request.TaxId.Trim(),
            billingEmail,
            string.IsNullOrWhiteSpace(request.Phone) ? "0000000000" : request.Phone.Trim(),
            string.IsNullOrWhiteSpace(request.Address) ? "Por definir" : request.Address.Trim(),
            string.IsNullOrWhiteSpace(request.City) ? "Bogotá" : request.City.Trim(),
            string.IsNullOrWhiteSpace(request.Department) ? "Cundinamarca" : request.Department.Trim(),
            plan,
            _clock.UtcNow);

        // Admin-created schools start active (no payment queue).
        profile.ActivateOrRenew(plan, _clock.UtcNow);

        await _profiles.AddAsync(profile, ct);
        await _events.AddAsync(
            MembershipEvent.Create(
                user.Id,
                MembershipEventTypes.Activated,
                plan.Code,
                plan.PriceCop,
                actorUserId,
                "Creada por administrador",
                _clock.UtcNow),
            ct);
        await _profiles.SaveChangesAsync(ct);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            Roles.School,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt);
    }

    private static void Validate(CreateSchoolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LegalName))
        {
            throw new DomainException("El nombre de la escuela es obligatorio.", 400, "invalid_name");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new DomainException("El correo es obligatorio.", 400, "invalid_email");
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
