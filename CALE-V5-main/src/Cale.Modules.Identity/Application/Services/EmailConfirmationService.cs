using System.Security.Cryptography;
using System.Text;
using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Email;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cale.Modules.Identity.Application.Services;

public sealed record EmailIssueResult(
    bool EmailSent,
    bool AutoConfirmed,
    string? DevConfirmationCode = null);

public sealed class EmailConfirmationService
{
    private readonly IUserStore _users;
    private readonly IEmailSender _email;
    private readonly IClock _clock;
    private readonly EmailOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EmailConfirmationService> _logger;

    public EmailConfirmationService(
        IUserStore users,
        IEmailSender email,
        IClock clock,
        IOptions<EmailOptions> options,
        IHostEnvironment environment,
        ILogger<EmailConfirmationService> logger)
    {
        _users = users;
        _email = email;
        _clock = clock;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public bool IsEmailDeliveryConfigured => _email.IsConfigured;

    public async Task<EmailIssueResult> IssueAndSendAsync(User user, CancellationToken ct)
    {
        if (!_email.IsConfigured)
        {
            if (_options.AutoConfirmWhenUnavailable)
            {
                user.MarkEmailConfirmed();
                await _users.SaveChangesAsync(ct);
                _logger.LogWarning(
                    "Email SMTP not configured; auto-confirmed userId={UserId} email={Email}",
                    user.Id,
                    user.Email);
                return new EmailIssueResult(EmailSent: false, AutoConfirmed: true);
            }

            return await IssuePendingWithoutDeliveryAsync(user, ct);
        }

        var code = GenerateCode(_options.CodeLength < 4 ? 6 : _options.CodeLength);
        var minutes = _options.CodeExpiresMinutes <= 0 ? 15 : _options.CodeExpiresMinutes;
        var expires = _clock.UtcNow.AddMinutes(minutes);

        user.BeginEmailConfirmation(HashCode(code), expires);
        await _users.SaveChangesAsync(ct);

        var body =
            $"Hola {user.Name},\n\n" +
            $"Tu código de verificación de Mi CALE es: {code}\n\n" +
            $"Caduca en {minutes} minutos.\n" +
            "Si no creaste esta cuenta, ignora este mensaje.\n";

        try
        {
            await _email.SendAsync(
                user.Email,
                "Código de verificación — Mi CALE",
                body,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SMTP send failed for userId={UserId} email={Email}",
                user.Id,
                user.Email);
            throw new DomainException(
                "No pudimos enviar el correo de verificación. Revisa la configuración SMTP del servidor o intenta más tarde.",
                503,
                "email_delivery_failed");
        }

        return new EmailIssueResult(EmailSent: true, AutoConfirmed: false);
    }

    public async Task ConfirmAsync(string email, string code, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(email, ct)
            ?? throw new DomainException("User not found.", 404, "user_not_found");

        if (user.EmailConfirmed)
        {
            return;
        }

        user.ConfirmEmailWithCode(HashCode(code.Trim()), _clock.UtcNow);
        await _users.SaveChangesAsync(ct);
    }

    public async Task<EmailIssueResult> ResendAsync(string email, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(email, ct)
            ?? throw new DomainException("User not found.", 404, "user_not_found");

        if (user.EmailConfirmed)
        {
            throw new DomainException(
                "Email already confirmed.",
                400,
                "email_already_confirmed");
        }

        return await IssueAndSendAsync(user, ct);
    }

    public static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
        return Convert.ToHexString(bytes);
    }

    private async Task<EmailIssueResult> IssuePendingWithoutDeliveryAsync(
        User user,
        CancellationToken ct)
    {
        var code = GenerateCode(_options.CodeLength < 4 ? 6 : _options.CodeLength);
        var minutes = _options.CodeExpiresMinutes <= 0 ? 15 : _options.CodeExpiresMinutes;
        var expires = _clock.UtcNow.AddMinutes(minutes);

        user.BeginEmailConfirmation(HashCode(code), expires);
        await _users.SaveChangesAsync(ct);

        _logger.LogWarning(
            "EMAIL SMTP not configured — verification code for {Email}: {Code} (expires in {Minutes} min)",
            user.Email,
            code,
            minutes);

        var devCode = _environment.IsDevelopment() ? code : null;
        return new EmailIssueResult(
            EmailSent: false,
            AutoConfirmed: false,
            DevConfirmationCode: devCode);
    }

    private static string GenerateCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString($"D{length}");
    }
}
