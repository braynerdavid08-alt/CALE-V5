using System.Text;

namespace Cale.BuildingBlocks.Infrastructure.Email.Templates;

/// <summary>
/// Shared Luz Verde HTML shell for transactional emails (table layout for Outlook/Gmail/mobile).
/// Soft page background, green brand bar, centered white card, reusable content slot.
/// </summary>
public static class EmailLayout
{
    public const string BrandName = "Luz Verde";
    public const string Tagline = "Formación que impulsa tu camino";
    public const string ClosingLine = "Juntos construimos una movilidad más responsable y segura.";
    public const string NightBlue = "#0B1F3A";
    public const string AccentGreen = "#22C55E";
    public const string TextMuted = "#6B7280";
    public const string TextBody = "#1F2937";
    public const string Border = "#D1D5DB";
    public const string PageBg = "#F3F4F6";
    public const string CardBg = "#FFFFFF";
    public const string LinkBlue = "#2563EB";
    public const string PublicSiteUrl = "https://www.cealuzverde.com";
    public const string NotificationsEmail = "notificaciones@cealuzverde.com";

    /// <summary>
    /// Escapes HTML special characters while keeping Spanish accents readable in mail clients.
    /// </summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }

    /// <param name="logoSrc">
    /// Image URL for the header logo. Prefer <c>cid:luzverde-logo</c> with an embedded attachment;
    /// data URIs are only for local HTML previews.
    /// </param>
    public static string Wrap(
        string titleEscaped,
        string innerHtml,
        string? preheaderEscaped = null,
        string? logoSrc = null)
    {
        var brand = Escape(BrandName);
        var tagline = Escape(Tagline).ToUpperInvariant();
        var closing = Escape(ClosingLine);
        var mailEsc = Escape(NotificationsEmail);
        var privacyUrl = Escape(PublicSiteUrl.TrimEnd('/') + "/legal/privacy");
        var mailUrl = Escape("mailto:" + NotificationsEmail);

        var preheaderBlock = string.IsNullOrWhiteSpace(preheaderEscaped)
            ? string.Empty
            : "<div style=\"display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;\">"
              + preheaderEscaped
              + "</div>";

        var logoBlock = string.IsNullOrWhiteSpace(logoSrc)
            ? string.Empty
            : "<img src=\"" + Escape(logoSrc) + "\" width=\"72\" height=\"72\" alt=\"" + brand
              + "\" style=\"display:block;margin:0 auto 12px auto;border:0;outline:none;text-decoration:none;width:72px;height:72px;\" />";

        var sb = new StringBuilder(5120);
        sb.Append("<!DOCTYPE html>");
        sb.Append("<html lang=\"es\">");
        sb.Append("<head>");
        sb.Append("<meta charset=\"utf-8\" />");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />");
        sb.Append("<title>").Append(titleEscaped).Append("</title>");
        sb.Append("<!--[if mso]><noscript><xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml></noscript><![endif]-->");
        sb.Append("</head>");
        sb.Append("<body style=\"margin:0;padding:0;background-color:")
            .Append(PageBg)
            .Append(";-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;\">");
        sb.Append(preheaderBlock);

        // Outer page
        sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"background-color:")
            .Append(PageBg)
            .Append(";\">");
        sb.Append("<tr><td align=\"center\" style=\"padding:28px 16px;\">");

        // Card shell max-width 560
        sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"max-width:560px;border-collapse:separate;\">");

        // Green brand bar
        sb.Append("<tr><td style=\"background-color:")
            .Append(AccentGreen)
            .Append(";height:4px;line-height:4px;font-size:0;border-radius:8px 8px 0 0;\">&nbsp;</td></tr>");

        // White card
        sb.Append("<tr><td style=\"background-color:")
            .Append(CardBg)
            .Append(";border:1px solid ")
            .Append(Border)
            .Append(";border-top:0;border-radius:0 0 8px 8px;\">");

        sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\">");

        // Header: logo + wordmark + tagline
        sb.Append("<tr><td align=\"center\" style=\"padding:28px 28px 8px 28px;\">");
        if (!string.IsNullOrEmpty(logoBlock))
        {
            sb.Append(logoBlock);
        }

        sb.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:22px;font-weight:700;line-height:1.25;color:")
            .Append(NightBlue)
            .Append(";margin:0 0 8px 0;\">")
            .Append(brand)
            .Append("</div>");
        sb.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:11px;font-weight:600;letter-spacing:0.14em;text-transform:uppercase;color:")
            .Append(TextMuted)
            .Append(";margin:0;\">")
            .Append(tagline)
            .Append("</div>");
        sb.Append("</td></tr>");

        // Divider under header
        sb.Append("<tr><td style=\"padding:16px 28px 0 28px;\">");
        sb.Append("<div style=\"height:1px;line-height:1px;background-color:#E5E7EB;width:100%;\">&nbsp;</div>");
        sb.Append("</td></tr>");

        // Content slot
        sb.Append("<tr><td align=\"left\" style=\"padding:24px 28px 8px 28px;font-family:Arial,Helvetica,sans-serif;color:")
            .Append(TextBody)
            .Append(";font-size:15px;line-height:1.55;\">");
        sb.Append(innerHtml);
        sb.Append("</td></tr>");

        // Closing
        sb.Append("<tr><td align=\"center\" style=\"padding:20px 28px 8px 28px;font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.5;color:")
            .Append(TextBody)
            .Append(";\">")
            .Append(closing)
            .Append("</td></tr>");

        // Footer links
        sb.Append("<tr><td align=\"center\" style=\"padding:8px 28px 28px 28px;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:1.6;color:")
            .Append(TextMuted)
            .Append(";\">");
        sb.Append(brand)
            .Append(" &nbsp;|&nbsp; ")
            .Append("<a href=\"")
            .Append(mailUrl)
            .Append("\" style=\"color:")
            .Append(LinkBlue)
            .Append(";text-decoration:underline;\">")
            .Append(mailEsc)
            .Append("</a>")
            .Append(" &nbsp;|&nbsp; ")
            .Append("<a href=\"")
            .Append(privacyUrl)
            .Append("\" style=\"color:")
            .Append(LinkBlue)
            .Append(";text-decoration:underline;\">Política de Privacidad</a>");
        sb.Append("</td></tr>");

        sb.Append("</table>"); // inner card
        sb.Append("</td></tr>"); // card cell
        sb.Append("</table>"); // card shell
        sb.Append("</td></tr></table>"); // page
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
