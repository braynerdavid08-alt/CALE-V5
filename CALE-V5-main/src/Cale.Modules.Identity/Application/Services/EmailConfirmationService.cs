using System.Security.Cryptography;
using System.Text;
using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Email;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Microsoft.Extensions.Options;

namespace Cale.Modules.Identity.Application.Services;

public sealed record EmailIssueResult(bool EmailSent, bool AutoConfirmed);

public sealed class EmailConfirmationService
{
    private readonly IUserStore _users;
    private readonly IEmailSender _email;
    private readonly IClock _clock;
    private readonly EmailOptions _options;

    public EmailConfirmationService(
        IUserStore users,
        IEmailSender email,
        IClock clock,
        IOptions<EmailOptions> options)
    {
        _users = users;
        _email = email;
        _clock = clock;
        _options = options.Value;
    }

    public bool IsEmailDeliveryConfigured => _email.IsConfigured;

    public async Task<EmailIssueResult> IssueAndSendAsync(User user, CancellationToken ct)
    {
        // Auto-confirm only when delivery is explicitly disabled (local/dev).
        // Enabled=true with a missing/broken provider must NOT confirm silently.
        if (!_email.IsConfigured)
        {
            if (_options.Enabled)
            {
                throw new DomainException(
                    "El envío de correo está activado pero no hay un proveedor configurado.",
                    503,
                    "email_delivery_misconfigured");
            }

            user.MarkEmailConfirmed();
            await _users.SaveChangesAsync(ct);
            return new EmailIssueResult(EmailSent: false, AutoConfirmed: true);
        }

        var code = GenerateCode(_options.CodeLength < 4 ? 6 : _options.CodeLength);
        var minutes = _options.CodeExpiresMinutes <= 0 ? 15 : _options.CodeExpiresMinutes;
        var expires = _clock.UtcNow.AddMinutes(minutes);

        user.BeginEmailConfirmation(HashCode(code), expires);
        await _users.SaveChangesAsync(ct);

        var body =
            $"Hola {user.Name},\n\n" +
            $"Tu código de verificación de Luz Verde es: {code}\n\n" +
            $"Caduca en {minutes} minutos.\n" +
            "Si no creaste esta cuenta, ignora este mensaje.\n";

        try
        {
            await _email.SendAsync(
                user.Email,
                "Código de verificación — Luz Verde",
                body,
                ct);
        }
        catch (DomainException)
        {
            // Keep pending confirmation so the user can resend; do not confirm.
            throw;
        }
        catch (Exception)
        {
            throw new DomainException(
                "No pudimos enviar el correo de verificación. Intenta reenviar el código en unos minutos.",
                503,
                "email_send_failed");
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

    private static string GenerateCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString($"D{length}");
    }
}
