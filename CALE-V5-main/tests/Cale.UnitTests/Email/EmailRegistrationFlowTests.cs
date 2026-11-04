using System.Text.RegularExpressions;
using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Email;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cale.UnitTests.Email;

/// <summary>
/// Full registration → confirm → login matrix for pending email accounts.
/// Uses an isolated in-memory store; never touches production data.
/// </summary>
public sealed class EmailRegistrationFlowTests : IDisposable
{
    private readonly IdentityTestFixture _fx = new();
    private readonly CapturingEmailSender _mail = new();
    private readonly EmailConfirmationService _emailSvc;
    private readonly RegisterUserHandler _register;
    private readonly ConfirmEmailHandler _confirm;
    private readonly ResendConfirmationHandler _resend;
    private readonly LoginUserHandler _login;

    public EmailRegistrationFlowTests()
    {
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            CodeLength = 6,
            CodeExpiresMinutes = 15
        });
        _emailSvc = new EmailConfirmationService(_fx.Users, _mail, _fx.Clock, options);
        _register = new RegisterUserHandler(_fx.Users, _fx.Hasher, _fx.Tokens, _fx.Clock, _emailSvc);
        _confirm = new ConfirmEmailHandler(_fx.Users, _fx.Tokens, _fx.Clock, _emailSvc);
        _resend = new ResendConfirmationHandler(_emailSvc);
        _login = new LoginUserHandler(
            _fx.Users,
            _fx.Hasher,
            _fx.Tokens,
            _fx.Clock,
            _fx.AccountStatus,
            NullLogger<LoginUserHandler>.Instance);
    }

    [Fact]
    public async Task Scenario01_Register_new_email_creates_pending_account_and_sends_code()
    {
        var result = await _register.HandleAsync(
            new RegisterRequest("Ana Nueva", "flow01@test.com", "Password1"),
            CancellationToken.None);

        Assert.True(result.RequiresEmailConfirmation);
        Assert.True(result.EmailSent);
        Assert.Null(result.Token);

        var user = await _fx.Users.FindByEmailAsync("flow01@test.com", CancellationToken.None);
        Assert.NotNull(user);
        Assert.False(user!.EmailConfirmed);
        Assert.False(string.IsNullOrWhiteSpace(user.EmailConfirmationCodeHash));
        Assert.Equal(6, LastCode().Length);
        Assert.Equal(1, _mail.SendCount);
    }

    [Fact]
    public async Task Scenario02_Register_again_while_pending_returns_email_pending_confirmation()
    {
        var req = new RegisterRequest("Ana", "flow02@test.com", "Password1");
        await _register.HandleAsync(req, CancellationToken.None);
        var beforeId = (await _fx.Users.FindByEmailAsync("flow02@test.com", CancellationToken.None))!.Id;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _register.HandleAsync(req, CancellationToken.None));

        Assert.Equal("email_pending_confirmation", ex.ErrorCode);
        var after = await _fx.Users.FindByEmailAsync("flow02@test.com", CancellationToken.None);
        Assert.NotNull(after);
        Assert.Equal(beforeId, after!.Id);
        Assert.False(after.EmailConfirmed);
    }

    [Fact]
    public async Task Scenario03_Resend_issues_new_code_without_duplicating_user()
    {
        await _register.HandleAsync(
            new RegisterRequest("Ana", "flow03@test.com", "Password1"),
            CancellationToken.None);
        var first = LastCode();
        var userId = (await _fx.Users.FindByEmailAsync("flow03@test.com", CancellationToken.None))!.Id;

        var resent = await _resend.HandleAsync(
            new ResendConfirmationRequest("flow03@test.com"),
            CancellationToken.None);

        Assert.True(resent.RequiresEmailConfirmation);
        Assert.True(resent.EmailSent);
        var second = LastCode();
        Assert.NotEqual(first, second);
        Assert.Equal(2, _mail.SendCount);

        var user = await _fx.Users.FindByEmailAsync("flow03@test.com", CancellationToken.None);
        Assert.Equal(userId, user!.Id);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task Scenario04_Old_code_rejected_after_resend()
    {
        await _register.HandleAsync(
            new RegisterRequest("Ana", "flow04@test.com", "Password1"),
            CancellationToken.None);
        var oldCode = LastCode();
        await _resend.HandleAsync(new ResendConfirmationRequest("flow04@test.com"), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _confirm.HandleAsync(new ConfirmEmailRequest("flow04@test.com", oldCode), CancellationToken.None));

        Assert.Equal("invalid_confirmation_code", ex.ErrorCode);
        var user = await _fx.Users.FindByEmailAsync("flow04@test.com", CancellationToken.None);
        Assert.False(user!.EmailConfirmed);
    }

    [Fact]
    public async Task Scenario05_Expired_code_rejected()
    {
        await _register.HandleAsync(
            new RegisterRequest("Ana", "flow05@test.com", "Password1"),
            CancellationToken.None);
        var code = LastCode();
        _fx.Clock.Advance(TimeSpan.FromMinutes(16));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _confirm.HandleAsync(new ConfirmEmailRequest("flow05@test.com", code), CancellationToken.None));

        Assert.Equal("confirmation_expired", ex.ErrorCode);
        Assert.False((await _fx.Users.FindByEmailAsync("flow05@test.com", CancellationToken.None))!.EmailConfirmed);
    }

    [Fact]
    public async Task Scenario06_Wrong_code_rejected()
    {
        await _register.HandleAsync(
            new RegisterRequest("Ana", "flow06@test.com", "Password1"),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _confirm.HandleAsync(new ConfirmEmailRequest("flow06@test.com", "000000"), CancellationToken.None));

        Assert.Equal("invalid_confirmation_code", ex.ErrorCode);
        Assert.False((await _fx.Users.FindByEmailAsync("flow06@test.com", CancellationToken.None))!.EmailConfirmed);
    }

    [Fact]
    public async Task Scenario07_Register_again_after_verified_returns_email_taken_same_user()
    {
        await _register.HandleAsync(
            new RegisterRequest("Ana", "flow07@test.com", "Password1"),
            CancellationToken.None);
        await _confirm.HandleAsync(
            new ConfirmEmailRequest("flow07@test.com", LastCode()),
            CancellationToken.None);
        var id = (await _fx.Users.FindByEmailAsync("flow07@test.com", CancellationToken.None))!.Id;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _register.HandleAsync(
                new RegisterRequest("Otra", "flow07@test.com", "Password1"),
                CancellationToken.None));

        Assert.Equal("email_taken", ex.ErrorCode);
        var user = await _fx.Users.FindByEmailAsync("flow07@test.com", CancellationToken.None);
        Assert.Equal(id, user!.Id);
        Assert.True(user.EmailConfirmed);
        Assert.Equal("Ana", user.Name);
    }

    [Fact]
    public async Task Scenario08_Login_without_verification_forbidden()
    {
        await _register.HandleAsync(
            new RegisterRequest("Ana", "flow08@test.com", "Password1"),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _login.HandleAsync(new LoginRequest("flow08@test.com", "Password1"), CancellationToken.None));

        Assert.Equal("email_not_confirmed", ex.ErrorCode);
    }

    [Fact]
    public async Task Scenario09_Login_succeeds_after_correct_verification()
    {
        await _register.HandleAsync(
            new RegisterRequest("Ana", "flow09@test.com", "Password1"),
            CancellationToken.None);
        var code = LastCode();

        var confirmed = await _confirm.HandleAsync(
            new ConfirmEmailRequest("flow09@test.com", code),
            CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(confirmed.Token));

        var login = await _login.HandleAsync(
            new LoginRequest("flow09@test.com", "Password1"),
            CancellationToken.None);
        Assert.Equal("Ana", login.Name);
        Assert.True((await _fx.Users.FindByEmailAsync("flow09@test.com", CancellationToken.None))!.EmailConfirmed);
    }

    [Fact]
    public async Task Resend_after_expiry_allows_new_code_then_login()
    {
        await _register.HandleAsync(
            new RegisterRequest("Ana", "flow10@test.com", "Password1"),
            CancellationToken.None);
        var expired = LastCode();
        _fx.Clock.Advance(TimeSpan.FromMinutes(16));

        await Assert.ThrowsAsync<DomainException>(() =>
            _confirm.HandleAsync(new ConfirmEmailRequest("flow10@test.com", expired), CancellationToken.None));

        await _resend.HandleAsync(new ResendConfirmationRequest("flow10@test.com"), CancellationToken.None);
        var fresh = LastCode();
        Assert.NotEqual(expired, fresh);

        await _confirm.HandleAsync(new ConfirmEmailRequest("flow10@test.com", fresh), CancellationToken.None);
        var login = await _login.HandleAsync(
            new LoginRequest("flow10@test.com", "Password1"),
            CancellationToken.None);
        Assert.Equal("Ana", login.Name);
    }

    private string LastCode()
    {
        Assert.False(string.IsNullOrWhiteSpace(_mail.LastPlainText));
        var match = Regex.Match(_mail.LastPlainText!, @"(?m)^(\d{6})$");
        Assert.True(match.Success, "Expected a 6-digit code line in the email body.");
        return match.Groups[1].Value;
    }

    public void Dispose() => _fx.Dispose();

    private sealed class CapturingEmailSender : IEmailSender
    {
        public bool IsConfigured => true;
        public int SendCount { get; private set; }
        public string? LastPlainText { get; private set; }

        public Task SendAsync(
            string toEmail,
            string subject,
            string plainTextBody,
            CancellationToken ct = default,
            string? htmlBody = null,
            IReadOnlyList<EmailEmbeddedImage>? embeddedImages = null)
        {
            SendCount++;
            LastPlainText = plainTextBody;
            return Task.CompletedTask;
        }
    }
}
