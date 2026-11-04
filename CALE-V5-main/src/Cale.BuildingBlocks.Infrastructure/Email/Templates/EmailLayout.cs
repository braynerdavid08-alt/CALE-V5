using System.Text;

namespace Cale.BuildingBlocks.Infrastructure.Email.Templates;

/// <summary>
/// Shared Luz Verde HTML shell for transactional emails (table layout for Outlook/Gmail).
/// Clean centered card inspired by Hostinger Reach-style messages.
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
    public const string PageBg = "#FFFFFF";
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
            : "<img src=\"" + Escape(logoSrc) + "\" width=\"88\" height=\"88\" alt=\"" + brand
              + "\" style=\"display:block;margin:0 auto 20px auto;border:0;outline:none;text-decoration:none;width:88px;height:88px;\" />";

        var sb = new StringBuilder(4096);
        sb.Append("<!DOCTYPE html>");
        sb.Append("<html lang=\"es\">");
        sb.Append("<head>");
        sb.Append("<meta charset=\"utf-8\" />");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />");
        sb.Append("<title>").Append(titleEscaped).Append("</title>");
        sb.Append("</head>");
        sb.Append("<body style=\"margin:0;padding:0;background-color:")
            .Append(PageBg)
            .Append(";-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;\">");
        sb.Append(preheaderBlock);
        sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"background-color:")
            .Append(PageBg)
            .Append(";\">");
        sb.Append("<tr><td align=\"center\" style=\"padding:32px 16px;\">");
        sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"max-width:520px;\">");

        // Tagline
        sb.Append("<tr><td align=\"center\" style=\"font-family:Arial,Helvetica,sans-serif;font-size:11px;font-weight:600;letter-spacing:0.14em;text-transform:uppercase;color:")
            .Append(TextMuted)
            .Append(";padding:0 12px 18px 12px;\">")
            .Append(tagline)
            .Append("</td></tr>");

        // Logo
        if (!string.IsNullOrEmpty(logoBlock))
        {
            sb.Append("<tr><td align=\"center\" style=\"padding:0 12px 8px 12px;\">")
                .Append(logoBlock)
                .Append("</td></tr>");
        }

        // Body
        sb.Append("<tr><td align=\"center\" style=\"padding:8px 12px 8px 12px;font-family:Arial,Helvetica,sans-serif;color:")
            .Append(TextBody)
            .Append(";font-size:15px;line-height:1.55;\">");
        sb.Append(innerHtml);
        sb.Append("</td></tr>");

        // Closing + footer
        sb.Append("<tr><td align=\"center\" style=\"padding:24px 12px 0 12px;\">");
        sb.Append("<div style=\"height:1px;line-height:1px;background-color:#E5E7EB;width:100%;\">&nbsp;</div>");
        sb.Append("</td></tr>");

        sb.Append("<tr><td align=\"center\" style=\"padding:20px 12px 8px 12px;font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.5;color:")
            .Append(TextBody)
            .Append(";\">")
            .Append(closing)
            .Append("</td></tr>");

        sb.Append("<tr><td align=\"center\" style=\"padding:8px 12px 24px 12px;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:1.6;color:")
            .Append(TextMuted)
            .Append(";\">");
        sb.Append(brand)
            .Append(" &nbsp;|&nbsp; ")
            .Append("<a href=\"")
            .Append(mailUrl)
            .Append("\" style=\"color:#2563EB;text-decoration:underline;\">")
            .Append(mailEsc)
            .Append("</a>")
            .Append(" &nbsp;|&nbsp; ")
            .Append("<a href=\"")
            .Append(privacyUrl)
            .Append("\" style=\"color:#2563EB;text-decoration:underline;\">Política de Privacidad</a>");
        sb.Append("</td></tr>");

        sb.Append("</table></td></tr></table>");
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
