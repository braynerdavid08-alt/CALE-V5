using Cale.BuildingBlocks.Domain.Email;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Email;

/// <summary>
/// Development fallback: writes the message to logs so local flows work without SMTP.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) =>
        _logger = logger;

    public bool IsConfigured => false;

    public Task SendAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "EMAIL (no SMTP configured) to={To} subject={Subject}\n{Body}",
            toEmail,
            subject,
            plainTextBody);
        return Task.CompletedTask;
    }
}
