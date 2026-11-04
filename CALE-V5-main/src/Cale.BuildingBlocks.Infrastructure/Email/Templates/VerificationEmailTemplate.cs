using System.Globalization;
using System.Text;
using Cale.BuildingBlocks.Domain.Email;

namespace Cale.BuildingBlocks.Infrastructure.Email.Templates;

/// <summary>
/// HTML + plain-text content for account email verification codes.
/// Does not generate or validate codes — only renders escaped content.
/// </summary>
public static class VerificationEmailTemplate
{
    public const string Subject = "Código de verificación – Luz Verde";

    public static EmailMessageContent Build(
        string recipientName,
        string verificationCode,
        int expiresMinutes,
        bool preferDataUriLogo = false)
    {
        var displayName = FormatDisplayName(recipientName);
        var minutes = expiresMinutes <= 0 ? 15 : expiresMinutes;
        var code = (verificationCode ?? string.Empty).Trim();
        var spacedCode = FormatSpacedCode(code);

        var nameEsc = EmailLayout.Escape(displayName);
        var codeEsc = EmailLayout.Escape(spacedCode);
        var minutesEsc = EmailLayout.Escape(minutes.ToString(CultureInfo.InvariantCulture));
        var preheader = EmailLayout.Escape(
            $"Tu código de verificación de Luz Verde vence en {minutes} minutos.");

        var logo = EmailBrandAssets.TryCreateLogo();
        var embedded = logo is null
            ? Array.Empty<EmailEmbeddedImage>()
            : new[] { logo };
        var logoSrc = preferDataUriLogo
            ? EmailBrandAssets.TryCreateLogoDataUri()
            : logo is null ? null : "cid:" + EmailBrandAssets.LogoContentId;

        var inner = $"""
            <h1 style="margin:0 0 16px 0;font-family:Arial,Helvetica,sans-serif;font-size:24px;font-weight:700;line-height:1.3;color:{EmailLayout.NightBlue};text-align:center;">
              Verifica tu cuenta
            </h1>
            <p style="margin:0 0 8px 0;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.55;color:{EmailLayout.TextBody};text-align:center;">
              Hola {nameEsc},
            </p>
            <p style="margin:0 0 22px 0;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.55;color:{EmailLayout.TextBody};text-align:center;">
              Gracias por registrarte. Para completar tu acceso, utiliza el siguiente código de verificación:
            </p>
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:0 0 18px 0;">
              <tr>
                <td align="center" style="background-color:#F9FAFB;border:1px solid {EmailLayout.Border};border-left:4px solid {EmailLayout.AccentGreen};border-radius:8px;padding:18px 20px;">
                  <div style="font-family:Arial,Helvetica,sans-serif;font-size:12px;font-weight:600;letter-spacing:0.08em;text-transform:uppercase;color:{EmailLayout.TextMuted};margin:0 0 10px 0;">
                    Código de verificación
                  </div>
                  <div style="font-family:Arial,Helvetica,sans-serif;font-size:30px;font-weight:700;letter-spacing:0.35em;color:{EmailLayout.NightBlue};line-height:1.2;padding-left:0.35em;">
                    {codeEsc}
                  </div>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 12px 0;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:1.5;color:{EmailLayout.TextMuted};text-align:center;">
              Este código expirará en <strong style="color:{EmailLayout.TextBody};">{minutesEsc} minutos</strong> por motivos de seguridad.
            </p>
            <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:1.5;color:{EmailLayout.TextMuted};text-align:center;">
              No compartas este código con nadie. Si no solicitaste esta verificación, ignora este mensaje.
            </p>
            """;

        var html = EmailLayout.Wrap(
            EmailLayout.Escape(Subject),
            inner,
            preheader,
            logoSrc);

        var plain = new StringBuilder()
            .Append("Hola ").Append(displayName).Append(',').Append('\n').Append('\n')
            .Append("Gracias por registrarte en Luz Verde.").Append('\n')
            .Append("Para completar tu acceso, utiliza el siguiente código de verificación:").Append('\n').Append('\n')
            .Append(code).Append('\n').Append('\n')
            .Append("Este código expirará en ").Append(minutes).Append(" minutos por motivos de seguridad.").Append('\n').Append('\n')
            .Append("No compartas este código con nadie. Si no solicitaste esta verificación, ignora este mensaje.").Append('\n').Append('\n')
            .Append(EmailLayout.ClosingLine).Append('\n').Append('\n')
            .Append("Luz Verde — ").Append(EmailLayout.Tagline).Append('\n')
            .ToString();

        return new EmailMessageContent(Subject, plain, html, embedded);
    }

    public static string FormatSpacedCode(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        return string.Join(' ', code.ToCharArray());
    }

    /// <summary>Title-cases each word for a polite Spanish greeting.</summary>
    public static string FormatDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "estudiante";
        }

        var textInfo = CultureInfo.GetCultureInfo("es-CO").TextInfo;
        var trimmed = name.Trim();
        return textInfo.ToTitleCase(trimmed.ToLower(CultureInfo.GetCultureInfo("es-CO")));
    }
}
