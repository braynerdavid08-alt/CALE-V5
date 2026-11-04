using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Privacy;
using Cale.BuildingBlocks.Infrastructure.Diagnostics;
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
        CancellationToken ct = default,
        string? htmlBody = null,
        IReadOnlyList<EmailEmbeddedImage>? embeddedImages = null)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.From, _options.FromName),
            Subject = subject
        };
        message.To.Add(toEmail);

        if (!string.IsNullOrWhiteSpace(htmlBody))
        {
            var plainView = AlternateView.CreateAlternateViewFromString(
                plainTextBody,
                Encoding.UTF8,
                MediaTypeNames.Text.Plain);
            var htmlView = AlternateView.CreateAlternateViewFromString(
                htmlBody,
                Encoding.UTF8,
                MediaTypeNames.Text.Html);

            if (embeddedImages is not null)
            {
                foreach (var img in embeddedImages)
                {
                    var stream = new MemoryStream(img.Content);
                    var resource = new LinkedResource(stream, img.ContentType)
                    {
                        ContentId = img.ContentId,
                        TransferEncoding = TransferEncoding.Base64
                    };
                    htmlView.LinkedResources.Add(resource);
                }
            }

            message.AlternateViews.Add(plainView);
            message.AlternateViews.Add(htmlView);
        }
        else
        {
            message.Body = plainTextBody;
            message.IsBodyHtml = false;
        }

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

        var approxBytes = Encoding.UTF8.GetByteCount(subject)
            + Encoding.UTF8.GetByteCount(plainTextBody)
            + Encoding.UTF8.GetByteCount(htmlBody ?? "")
            + (embeddedImages?.Sum(i => i.Content.Length) ?? 0);
        try
        {
            await client.SendMailAsync(message, ct);
            OutboundTraffic.Record("smtp", approxBytes, 0);
            _logger.LogInformation("Email sent to={To} subject={Subject}", PersonalData.MaskEmail(toEmail), subject);
        }
        catch (Exception ex)
        {
            OutboundTraffic.Record("smtp", approxBytes, 0, failed: true);
            _logger.LogError(ex, "Failed to send email to={To}", PersonalData.MaskEmail(toEmail));
            throw;
        }
    }
}
