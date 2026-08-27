using Cale.BuildingBlocks.Domain.Email;
using Microsoft.Extensions.Options;

namespace Cale.BuildingBlocks.Infrastructure.Email;

/// <summary>Uses SMTP when configured; otherwise logs the email.</summary>
public sealed class ConfigurableEmailSender : IEmailSender
{
    private readonly IEmailSender _inner;

    public ConfigurableEmailSender(
        IOptions<EmailOptions> options,
        SmtpEmailSender smtp,
        LoggingEmailSender logging)
    {
        var cfg = options.Value;
        var smtpReady = cfg.Enabled
            && !string.IsNullOrWhiteSpace(cfg.Smtp.Host);
        _inner = smtpReady ? smtp : logging;
    }

    public bool IsConfigured => _inner.IsConfigured;

    public Task SendAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken ct = default) =>
        _inner.SendAsync(toEmail, subject, plainTextBody, ct);
}
