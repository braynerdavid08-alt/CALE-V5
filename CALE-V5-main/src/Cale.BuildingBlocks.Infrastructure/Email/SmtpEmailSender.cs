using Cale.BuildingBlocks.Domain.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

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
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.From));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = plainTextBody };

        using var client = new SmtpClient();
        var socketOptions = ResolveSocketOptions(_options.Smtp.Port, _options.Smtp.UseSsl);

        try
        {
            await client.ConnectAsync(
                _options.Smtp.Host,
                _options.Smtp.Port,
                socketOptions,
                ct);

            if (!string.IsNullOrWhiteSpace(_options.Smtp.User))
            {
                await client.AuthenticateAsync(
                    _options.Smtp.User,
                    _options.Smtp.Password,
                    ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(quit: true, ct);
            _logger.LogInformation("Email sent to={To} subject={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email to={To} host={Host} port={Port}",
                toEmail,
                _options.Smtp.Host,
                _options.Smtp.Port);
            throw;
        }
    }

    private static SecureSocketOptions ResolveSocketOptions(int port, bool useSsl) =>
        port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto
        };
}
