using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Email;
using Cale.BuildingBlocks.Infrastructure.Email.Templates;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cale.UnitTests.Email;

public sealed class EmailConfirmationServiceTests
{
    [Fact]
    public async Task IssueAndSend_when_disabled_and_unconfigured_auto_confirms()
    {
        var fx = new IdentityTestFixture();
        var user = User.RegisterStudent("Ana", "ana@test.com", "hash", fx.Clock.UtcNow);
        await fx.Users.AddAsync(user, CancellationToken.None);
        await fx.Users.SaveChangesAsync(CancellationToken.None);

        var result = await fx.EmailConfirmation.IssueAndSendAsync(user, CancellationToken.None);

        Assert.True(result.AutoConfirmed);
        Assert.False(result.EmailSent);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task IssueAndSend_when_enabled_but_sender_unconfigured_does_not_auto_confirm()
    {
        var fx = new IdentityTestFixture();
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            CodeLength = 6,
            CodeExpiresMinutes = 15
        });
        var svc = new EmailConfirmationService(
            fx.Users,
            new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance),
            fx.Clock,
            options);

        var user = User.RegisterStudent("Ana", "ana2@test.com", "hash", fx.Clock.UtcNow);
        await fx.Users.AddAsync(user, CancellationToken.None);
        await fx.Users.SaveChangesAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.IssueAndSendAsync(user, CancellationToken.None));

        Assert.Equal("email_delivery_misconfigured", ex.ErrorCode);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task IssueAndSend_send_failure_keeps_user_pending()
    {
        var fx = new IdentityTestFixture();
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            CodeLength = 6,
            CodeExpiresMinutes = 15
        });
        var failing = new FailingEmailSender();
        var svc = new EmailConfirmationService(fx.Users, failing, fx.Clock, options);

        var user = User.RegisterStudent("Ana", "ana3@test.com", "hash", fx.Clock.UtcNow);
        await fx.Users.AddAsync(user, CancellationToken.None);
        await fx.Users.SaveChangesAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.IssueAndSendAsync(user, CancellationToken.None));

        Assert.Equal("email_send_failed", ex.ErrorCode);
        Assert.False(user.EmailConfirmed);
        Assert.False(string.IsNullOrWhiteSpace(user.EmailConfirmationCodeHash));
    }

    [Fact]
    public async Task IssueAndSend_uses_html_verification_template_without_changing_hash_rules()
    {
        var fx = new IdentityTestFixture();
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            CodeLength = 6,
            CodeExpiresMinutes = 15
        });
        var capturing = new CapturingEmailSender();
        var svc = new EmailConfirmationService(fx.Users, capturing, fx.Clock, options);

        var user = User.RegisterStudent("braynner", "ana-html@test.com", "hash", fx.Clock.UtcNow);
        await fx.Users.AddAsync(user, CancellationToken.None);
        await fx.Users.SaveChangesAsync(CancellationToken.None);

        var result = await svc.IssueAndSendAsync(user, CancellationToken.None);

        Assert.True(result.EmailSent);
        Assert.False(result.AutoConfirmed);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(VerificationEmailTemplate.Subject, capturing.Subject);
        Assert.Contains("Hola Braynner", capturing.PlainText, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(capturing.Html));
        Assert.Contains("Verifica tu cuenta", capturing.Html!, StringComparison.Ordinal);
        Assert.Contains(EmailLayout.NightBlue, capturing.Html!, StringComparison.Ordinal);
        Assert.Contains("FORMACIÓN QUE IMPULSA TU CAMINO", capturing.Html!, StringComparison.Ordinal);
        Assert.Contains("cid:luzverde-logo", capturing.Html!, StringComparison.Ordinal);
        Assert.NotEmpty(capturing.EmbeddedImages);
        Assert.Equal(EmailBrandAssets.LogoContentId, capturing.EmbeddedImages[0].ContentId);

        // Code in the email still matches the stored hash (generation/validation unchanged).
        var code = System.Text.RegularExpressions.Regex.Match(
                capturing.PlainText!,
                @"(?m)^(\d{6})$")
            .Groups[1].Value;
        Assert.Equal(6, code.Length);
        Assert.Equal(EmailConfirmationService.HashCode(code), user.EmailConfirmationCodeHash);
    }

    [Fact]
    public async Task Confirm_with_valid_code_then_login_path_ok()
    {
        var fx = new IdentityTestFixture();
        await fx.CreateRegister().HandleAsync(
            new Cale.Modules.Identity.Application.DTOs.RegisterRequest("Ana", "ana4@test.com", "Password1"),
            CancellationToken.None);

        var user = await fx.Users.FindByEmailAsync("ana4@test.com", CancellationToken.None);
        Assert.NotNull(user);
        const string code = "424242";
        user!.BeginEmailConfirmation(
            EmailConfirmationService.HashCode(code),
            fx.Clock.UtcNow.AddMinutes(15));
        await fx.Users.SaveChangesAsync(CancellationToken.None);

        await fx.CreateConfirmEmail().HandleAsync(
            new Cale.Modules.Identity.Application.DTOs.ConfirmEmailRequest("ana4@test.com", code),
            CancellationToken.None);

        var login = await fx.CreateLogin().HandleAsync(
            new Cale.Modules.Identity.Application.DTOs.LoginRequest("ana4@test.com", "Password1"),
            CancellationToken.None);
        Assert.Equal("Ana", login.Name);
    }

    [Fact]
    public async Task Login_unconfirmed_is_forbidden()
    {
        var fx = new IdentityTestFixture();
        await fx.CreateRegister().HandleAsync(
            new Cale.Modules.Identity.Application.DTOs.RegisterRequest("Ana", "ana5@test.com", "Password1"),
            CancellationToken.None);

        var user = await fx.Users.FindByEmailAsync("ana5@test.com", CancellationToken.None);
        Assert.NotNull(user);
        user!.BeginEmailConfirmation(
            EmailConfirmationService.HashCode("111111"),
            fx.Clock.UtcNow.AddMinutes(15));
        await fx.Users.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fx.CreateLogin().HandleAsync(
                new Cale.Modules.Identity.Application.DTOs.LoginRequest("ana5@test.com", "Password1"),
                CancellationToken.None));
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public bool IsConfigured => true;

        public Task SendAsync(
            string toEmail,
            string subject,
            string plainTextBody,
            CancellationToken ct = default,
            string? htmlBody = null,
            IReadOnlyList<EmailEmbeddedImage>? embeddedImages = null) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public bool IsConfigured => true;
        public string? Subject { get; private set; }
        public string? PlainText { get; private set; }
        public string? Html { get; private set; }
        public IReadOnlyList<EmailEmbeddedImage> EmbeddedImages { get; private set; } =
            Array.Empty<EmailEmbeddedImage>();

        public Task SendAsync(
            string toEmail,
            string subject,
            string plainTextBody,
            CancellationToken ct = default,
            string? htmlBody = null,
            IReadOnlyList<EmailEmbeddedImage>? embeddedImages = null)
        {
            Subject = subject;
            PlainText = plainTextBody;
            Html = htmlBody;
            EmbeddedImages = embeddedImages ?? Array.Empty<EmailEmbeddedImage>();
            return Task.CompletedTask;
        }
    }
}
