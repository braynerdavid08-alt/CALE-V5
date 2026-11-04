namespace Cale.BuildingBlocks.Domain.Email;

public interface IEmailSender
{
    /// <summary>True when a real SMTP (or provider) is configured and enabled.</summary>
    bool IsConfigured { get; }

    /// <param name="plainTextBody">Required plain-text fallback for clients that ignore HTML.</param>
    /// <param name="htmlBody">Optional HTML body (Hostinger <c>html</c> / SMTP alternate view).</param>
    /// <param name="embeddedImages">Optional CID images referenced from HTML as <c>cid:...</c>.</param>
    Task SendAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken ct = default,
        string? htmlBody = null,
        IReadOnlyList<EmailEmbeddedImage>? embeddedImages = null);
}
