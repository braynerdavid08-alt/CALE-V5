using Cale.BuildingBlocks.Infrastructure.Email.Templates;

namespace Cale.UnitTests.Email;

public sealed class VerificationEmailTemplateTests
{
    [Fact]
    public void Build_uses_required_subject_and_branding()
    {
        var msg = VerificationEmailTemplate.Build("ana maría", "123456", 15);

        Assert.Equal(VerificationEmailTemplate.Subject, msg.Subject);
        Assert.Equal("Código de verificación – Luz Verde", msg.Subject);
        Assert.Contains("FORMACIÓN QUE IMPULSA TU CAMINO", msg.Html, StringComparison.Ordinal);
        Assert.Contains("Verifica tu cuenta", msg.Html, StringComparison.Ordinal);
        Assert.Contains(EmailLayout.NightBlue, msg.Html, StringComparison.Ordinal);
        Assert.Contains("Luz Verde", msg.Html, StringComparison.Ordinal);
        Assert.Contains(EmailLayout.ClosingLine, msg.Html, StringComparison.Ordinal);
        Assert.Contains("cid:luzverde-logo", msg.Html, StringComparison.Ordinal);
        Assert.Contains("/legal/privacy", msg.Html, StringComparison.Ordinal);
        Assert.NotEmpty(msg.EmbeddedImages);
        Assert.True(msg.EmbeddedImages[0].Content.Length > 0);
    }

    [Fact]
    public void Build_preferDataUriLogo_embeds_base64_for_previews()
    {
        var msg = VerificationEmailTemplate.Build("Ana", "123456", 15, preferDataUriLogo: true);
        Assert.Contains("data:image/png;base64,", msg.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_title_cases_name_and_escapes_html()
    {
        var msg = VerificationEmailTemplate.Build(
            "juan <script>alert(1)</script>",
            "99<script>",
            15);

        Assert.Contains("Hola Juan &lt;Script&gt;Alert(1)&lt;/Script&gt;", msg.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", msg.Html, StringComparison.OrdinalIgnoreCase);
        // Code digits are spaced in HTML and special chars are escaped.
        Assert.Contains("9 9 &lt; s c r i p t &gt;", msg.Html, StringComparison.Ordinal);
        Assert.Contains("Hola Juan <Script>Alert(1)</Script>", msg.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_includes_code_expiry_and_warnings()
    {
        var msg = VerificationEmailTemplate.Build("Braynner", "424242", 15);

        Assert.Contains("4 2 4 2 4 2", msg.Html, StringComparison.Ordinal);
        Assert.Contains("424242", msg.PlainText, StringComparison.Ordinal);
        Assert.Contains("15 minutos", msg.Html, StringComparison.Ordinal);
        Assert.Contains("No compartas este código", msg.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no solicitaste", msg.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("código de verificación", msg.PlainText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, "estudiante")]
    [InlineData("", "estudiante")]
    [InlineData("  ", "estudiante")]
    [InlineData("braynner david", "Braynner David")]
    public void FormatDisplayName_normalizes(string? input, string expected)
    {
        Assert.Equal(expected, VerificationEmailTemplate.FormatDisplayName(input));
    }
}
