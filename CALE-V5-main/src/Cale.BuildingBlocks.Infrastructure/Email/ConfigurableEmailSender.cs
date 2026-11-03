using Cale.BuildingBlocks.Domain.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cale.BuildingBlocks.Infrastructure.Email;

/// <summary>
/// Resolves the active <see cref="IEmailSender"/> from <see cref="EmailOptions.Provider"/>.
/// HostingerMail never falls back to SMTP. Auto prefers Hostinger, then SMTP, then logging.
/// </summary>
public sealed class ConfigurableEmailSender : IEmailSender
{
    private readonly IEmailSender _inner;
    private readonly ILogger<ConfigurableEmailSender> _logger;

    public ConfigurableEmailSender(
        IOptions<EmailOptions> options,
        HostingerMailApiSender hostinger,
        SmtpEmailSender smtp,
        LoggingEmailSender logging,
        ILogger<ConfigurableEmailSender> logger)
    {
        _logger = logger;
        _inner = Resolve(options.Value, hostinger, smtp, logging);
    }

    public bool IsConfigured => _inner.IsConfigured;

    public Task SendAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken ct = default) =>
        _inner.SendAsync(toEmail, subject, plainTextBody, ct);

    private IEmailSender Resolve(
        EmailOptions cfg,
        HostingerMailApiSender hostinger,
        SmtpEmailSender smtp,
        LoggingEmailSender logging)
    {
        var provider = string.IsNullOrWhiteSpace(cfg.Provider)
            ? EmailProviders.Auto
            : cfg.Provider.Trim();

        if (provider.Equals(EmailProviders.HostingerMail, StringComparison.OrdinalIgnoreCase))
        {
            if (!cfg.Enabled || !cfg.HostingerMail.IsComplete)
            {
                throw new InvalidOperationException(
                    "Email:Provider=HostingerMail requires Email:Enabled=true, " +
                    "Email:HostingerMail:ApiToken and Email:HostingerMail:MailboxResourceId.");
            }

            _logger.LogInformation("Email delivery provider: HostingerMail API (HTTPS).");
            return hostinger;
        }

        if (provider.Equals(EmailProviders.Smtp, StringComparison.OrdinalIgnoreCase))
        {
            if (!SmtpReady(cfg))
            {
                throw new InvalidOperationException(
                    "Email:Provider=Smtp requires Email:Enabled=true and Email:Smtp:Host.");
            }

            _logger.LogInformation("Email delivery provider: SMTP.");
            return smtp;
        }

        if (!provider.Equals(EmailProviders.Auto, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Email:Provider '{provider}' is invalid. Use HostingerMail, Smtp, or Auto.");
        }

        // Auto: Hostinger → SMTP → logging (dev).
        if (cfg.Enabled && cfg.HostingerMail.IsComplete)
        {
            _logger.LogInformation("Email delivery provider: HostingerMail API (Auto).");
            return hostinger;
        }

        if (SmtpReady(cfg))
        {
            _logger.LogInformation("Email delivery provider: SMTP (Auto).");
            return smtp;
        }

        if (cfg.Enabled)
        {
            // Enabled but neither provider ready — do not silently log+auto-confirm.
            throw new InvalidOperationException(
                "Email:Enabled=true but no delivery provider is fully configured. " +
                "Set HostingerMail (ApiToken + MailboxResourceId) or Smtp:Host, " +
                "or set Email:Enabled=false for local auto-confirm.");
        }

        _logger.LogWarning("Email delivery provider: logging fallback (Enabled=false).");
        return logging;
    }

    private static bool SmtpReady(EmailOptions cfg) =>
        cfg.Enabled && !string.IsNullOrWhiteSpace(cfg.Smtp.Host);
}
