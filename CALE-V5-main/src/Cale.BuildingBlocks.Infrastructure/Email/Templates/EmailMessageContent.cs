using Cale.BuildingBlocks.Domain.Email;

namespace Cale.BuildingBlocks.Infrastructure.Email.Templates;

public sealed record EmailMessageContent(
    string Subject,
    string PlainText,
    string Html,
    IReadOnlyList<EmailEmbeddedImage> EmbeddedImages)
{
    public EmailMessageContent(string subject, string plainText, string html)
        : this(subject, plainText, html, Array.Empty<EmailEmbeddedImage>())
    {
    }
}
