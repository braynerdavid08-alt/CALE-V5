namespace Cale.BuildingBlocks.Domain.Email;

public interface IEmailSender
{
    /// <summary>True when a real SMTP (or provider) is configured and enabled.</summary>
    bool IsConfigured { get; }

    Task SendAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken ct = default);
}
