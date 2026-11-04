using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Privacy;
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
        CancellationToken ct = default,
        string? htmlBody = null,
        IReadOnlyList<EmailEmbeddedImage>? embeddedImages = null)
    {
        _logger.LogWarning(
            "EMAIL (no SMTP configured) to={To} subject={Subject} htmlChars={HtmlChars} embeds={Embeds}\n{Body}",
            PersonalData.MaskEmail(toEmail),
            subject,
            htmlBody?.Length ?? 0,
            embeddedImages?.Count ?? 0,
            plainTextBody);
        return Task.CompletedTask;
    }
}
