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

public sealed class RegisterUserHandler
{
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;
    private readonly EmailConfirmationService _emailConfirmation;

    public RegisterUserHandler(
        IUserStore users,
        IPasswordHasher hasher,
        ITokenService tokens,
        IClock clock,
        EmailConfirmationService emailConfirmation)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
        _emailConfirmation = emailConfirmation;
    }

    public async Task<PendingEmailConfirmationResponse> HandleAsync(
        RegisterRequest request,
        CancellationToken ct)
    {
        Validate(request);
        var email = EmailAddress.NormalizeForRegistration(request.Email);

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
        var issue = await _emailConfirmation.IssueAndSendAsync(user, ct);

        if (issue.AutoConfirmed)
        {
            user.RecordLogin(_clock.UtcNow);
            await _users.SaveChangesAsync(ct);
            var token = _tokens.Create(user.Id, user.Email, user.Name, Roles.Student);
            return new PendingEmailConfirmationResponse(
                user.Email,
                "Cuenta creada. El envío de correo no está configurado en el servidor; entraste sin código.",
                RequiresEmailConfirmation: false,
                EmailSent: false,
                Token: token,
                UserId: user.Id,
                Name: user.Name,
                Role: Roles.Student,
                MustChangePassword: false);
        }

        return BuildPendingResponse(user.Email, issue);

    private static PendingEmailConfirmationResponse BuildPendingResponse(
        string email,
        EmailIssueResult issue) =>
        new(
            email,
            issue.EmailSent
                ? "Te enviamos un código a tu correo. Confírmalo para activar la cuenta."
                : "El servidor no pudo enviar el correo. Configura SMTP en producción o usa el código de desarrollo si aplica.",
            RequiresEmailConfirmation: true,
            EmailSent: issue.EmailSent,
            DevConfirmationCode: issue.DevConfirmationCode);

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
