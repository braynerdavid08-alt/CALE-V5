using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class RegisterSchoolHandler
{
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IMembershipEventStore _events;
    private readonly ISchoolRegistrationRegistryStore _registry;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;
    private readonly EmailConfirmationService _emailConfirmation;

    public RegisterSchoolHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IMembershipEventStore events,
        ISchoolRegistrationRegistryStore registry,
        IPasswordHasher hasher,
        ITokenService tokens,
        IClock clock,
        EmailConfirmationService emailConfirmation)
    {
        _users = users;
        _profiles = profiles;
        _events = events;
        _registry = registry;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
        _emailConfirmation = emailConfirmation;
    }

    public async Task<PendingEmailConfirmationResponse> HandleAsync(
        RegisterSchoolRequest request,
        CancellationToken ct)
    {
        Validate(request);

        var claimFreeTrial = request.ClaimFreeTrial
            || SchoolPlans.Normalize(request.PlanCode) == SchoolPlans.Trial;

        var plan = claimFreeTrial
            ? SchoolPlans.TrialPlan
            : SchoolPlans.Find(request.PlanCode)
                ?? throw new DomainException("Invalid plan.", 400, "invalid_plan");

        if (claimFreeTrial && plan.Code != SchoolPlans.Trial)
        {
            throw new DomainException("Invalid trial plan.", 400, "invalid_plan");
        }

        var email = EmailAddress.NormalizeForRegistration(request.Email);
        var billingEmail = EmailAddress.NormalizeForRegistration(request.BillingEmail);

        if (await _users.ExistsByEmailAsync(email, ct))
        {
            throw new ConflictException(
                "Email already registered.",
                "email_taken");
        }

        var keys = BuildKeys(request, email, billingEmail);

        if (claimFreeTrial
            && await _registry.HasSimilarRegistrationAsync(
                keys.TaxIdKey,
                keys.BillingEmailKey,
                keys.AccessEmailKey,
                keys.PhoneKey,
                keys.LegalNameKey,
                keys.CityKey,
                ct))
        {
            throw new DomainException(
                "Esta escuela ya utilizó la prueba gratis o se registró antes con datos similares.",
                403,
                "free_trial_not_available");
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

        if (claimFreeTrial)
        {
            profile.ActivateOrRenew(plan, _clock.UtcNow);
            await _events.AddAsync(
                MembershipEvent.Create(
                    user.Id,
                    MembershipEventTypes.FreeTrialActivated,
                    plan.Code,
                    plan.PriceCop,
                    user.Id,
                    "Prueba gratis 1 mes al registrarse",
                    _clock.UtcNow),
                ct);
        }
        else
        {
            await _events.AddAsync(
                MembershipEvent.Create(
                    user.Id,
                    MembershipEventTypes.Requested,
                    plan.Code,
                    plan.PriceCop,
                    user.Id,
                    "Alta de escuela — pendiente de pago",
                    _clock.UtcNow),
                ct);
        }

        await _profiles.SaveChangesAsync(ct);

        await _registry.TouchExistingAsync(
            keys.TaxIdKey,
            keys.BillingEmailKey,
            keys.AccessEmailKey,
            keys.PhoneKey,
            keys.LegalNameKey,
            keys.CityKey,
            claimFreeTrial,
            user.Id,
            _clock.UtcNow,
            ct);

        var issue = await _emailConfirmation.IssueAndSendAsync(user, ct);

        if (issue.AutoConfirmed)
        {
            user.RecordLogin(_clock.UtcNow);
            await _users.SaveChangesAsync(ct);
            var token = _tokens.Create(user.Id, user.Email, user.Name, Roles.School);
            var message = claimFreeTrial
                ? "Cuenta creada con 1 mes gratis. Ya puedes usar Mi CALE."
                : "Cuenta creada. Contrata un plan y sube el comprobante para activar todas las funciones.";
            return new PendingEmailConfirmationResponse(
                user.Email,
                message,
                RequiresEmailConfirmation: false,
                EmailSent: false,
                Token: token,
                UserId: user.Id,
                Name: user.Name,
                Role: Roles.School,
                MustChangePassword: false);
        }

        var pendingMessage = claimFreeTrial
            ? "Te enviamos un código a tu correo. Al confirmarlo activarás tu mes gratis."
            : "Te enviamos un código a tu correo. Confírmalo y luego contrata tu plan en Membresía.";

        return new PendingEmailConfirmationResponse(
            user.Email,
            issue.EmailSent
                ? pendingMessage
                : "Cuenta creada, pero el servidor no pudo enviar el correo. Configura SMTP o contacta al administrador.",
            RequiresEmailConfirmation: true,
            EmailSent: issue.EmailSent,
            DevConfirmationCode: issue.DevConfirmationCode);
    }

    private static RegistrationKeys BuildKeys(
        RegisterSchoolRequest request,
        string accessEmail,
        string billingEmail) =>
        new(
            SchoolRegistrationKeys.NormalizeTaxId(request.TaxId),
            SchoolRegistrationKeys.NormalizeEmail(billingEmail),
            SchoolRegistrationKeys.NormalizeEmail(accessEmail),
            SchoolRegistrationKeys.NormalizePhone(request.Phone),
            SchoolRegistrationKeys.NormalizeLegalName(request.LegalName),
            SchoolRegistrationKeys.NormalizeCity(request.City));

    private sealed record RegistrationKeys(
        string TaxIdKey,
        string BillingEmailKey,
        string AccessEmailKey,
        string PhoneKey,
        string LegalNameKey,
        string CityKey);

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
