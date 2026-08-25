using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class RegisterSchoolHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;

    public RegisterSchoolHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IPasswordHasher hasher,
        ITokenService tokens,
        IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<AuthResponse> HandleAsync(
        RegisterSchoolRequest request,
        CancellationToken ct)
    {
        Validate(request);
        var plan = SchoolPlans.Find(request.PlanCode)
            ?? throw new DomainException("Invalid plan.", 400, "invalid_plan");

        var email = EmailAddress.Normalize(request.Email);
        var billingEmail = EmailAddress.Normalize(request.BillingEmail);

        if (await _users.ExistsByEmailAsync(email, ct))
        {
            throw new ConflictException(
                "Email already registered.",
                "email_taken");
        }

        var user = User.RegisterSchool(
            request.ContactName,
            email,
            _hasher.Hash(request.Password),
            _clock.UtcNow);

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        var profile = SchoolProfile.Create(
            user.Id,
            request.LegalName,
            request.TaxId,
            billingEmail,
            request.Phone,
            request.Address,
            request.City,
            request.Department,
            plan,
            _clock.UtcNow);

        await _profiles.AddAsync(profile, ct);
        await _profiles.SaveChangesAsync(ct);

        var token = _tokens.Create(
            user.Id,
            user.Email,
            user.Name,
            Roles.School);

        return new AuthResponse(
            token,
            user.Id,
            user.Name,
            user.Email,
            Roles.School);
    }

    private static void Validate(RegisterSchoolRequest request)
    {
        Require(request.ContactName, "Contact name is required.", "invalid_contact_name");
        Require(request.LegalName, "Legal name is required.", "invalid_legal_name");
        Require(request.TaxId, "Tax ID (NIT) is required.", "invalid_tax_id");
        Require(request.Phone, "Phone is required.", "invalid_phone");
        Require(request.Address, "Address is required.", "invalid_address");
        Require(request.City, "City is required.", "invalid_city");
        Require(request.Department, "Department is required.", "invalid_department");

        if (string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length < 8)
        {
            throw new DomainException(
                "Password must have at least 8 characters.",
                400,
                "weak_password");
        }

        if (string.IsNullOrWhiteSpace(request.BillingEmail))
        {
            throw new DomainException(
                "Billing email is required.",
                400,
                "invalid_billing_email");
        }
    }

    private static void Require(string? value, string message, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(message, 400, code);
        }
    }
}
