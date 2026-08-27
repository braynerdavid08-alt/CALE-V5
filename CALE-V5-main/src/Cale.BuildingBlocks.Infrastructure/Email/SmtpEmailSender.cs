using System.Net;
using System.Net.Mail;
using Cale.BuildingBlocks.Domain.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cale.BuildingBlocks.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        _options.Enabled && !string.IsNullOrWhiteSpace(_options.Smtp.Host);

    public async Task SendAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken ct = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.From, _options.FromName),
            Subject = subject,
            Body = plainTextBody,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
        {
            EnableSsl = _options.Smtp.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.Smtp.User))
        {
            client.Credentials = new NetworkCredential(
                _options.Smtp.User,
                _options.Smtp.Password);
        }

        try
        {
            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Email sent to={To} subject={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to={To}", toEmail);
            throw;
        }
    }
}
