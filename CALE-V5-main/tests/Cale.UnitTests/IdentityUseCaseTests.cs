using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;

namespace Cale.UnitTests;

public class IdentityUseCaseTests : IDisposable
{
    private readonly IdentityTestFixture _fx = new();

    [Fact]
    public async Task Register_CreatesPendingStudentWithoutToken()
    {
        var result = await _fx.CreateRegister().HandleAsync(
            new RegisterRequest("Ana", "ana@test.com", "Password1"),
            CancellationToken.None);

        Assert.Equal("ana@test.com", result.Email);
        var user = await _fx.Users.FindByEmailAsync("ana@test.com", CancellationToken.None);
        Assert.NotNull(user);
        Assert.Equal(Roles.Student, user!.Role);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        var handler = _fx.CreateRegister();
        var request = new RegisterRequest("Ana", "ana@test.com", "Password1");
        await handler.HandleAsync(request, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Login_Unconfirmed_ThrowsForbidden()
    {
        await _fx.CreateRegister().HandleAsync(
            new RegisterRequest("Ana", "ana@test.com", "Password1"),
            CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _fx.CreateLogin().HandleAsync(
                new LoginRequest("ana@test.com", "Password1"),
                CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmEmail_ThenLogin_Succeeds()
    {
        await _fx.CreateRegister().HandleAsync(
            new RegisterRequest("Ana", "ana@test.com", "Password1"),
            CancellationToken.None);

        var user = await _fx.Users.FindByEmailAsync("ana@test.com", CancellationToken.None);
        Assert.NotNull(user);
        // Force a known code for the test.
        const string code = "123456";
        user!.BeginEmailConfirmation(
            EmailConfirmationService.HashCode(code),
            _fx.Clock.UtcNow.AddMinutes(15));
        await _fx.Users.SaveChangesAsync(CancellationToken.None);

        var confirmed = await _fx.CreateConfirmEmail().HandleAsync(
            new ConfirmEmailRequest("ana@test.com", code),
            CancellationToken.None);

        Assert.Equal(Roles.Student, confirmed.Role);
        Assert.False(string.IsNullOrWhiteSpace(confirmed.Token));

        var login = await _fx.CreateLogin().HandleAsync(
            new LoginRequest("ana@test.com", "Password1"),
            CancellationToken.None);
        Assert.Equal("Ana", login.Name);
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        await _fx.CreateRegister().HandleAsync(
            new RegisterRequest("Ana", "ana@test.com", "Password1"),
            CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _fx.CreateLogin().HandleAsync(
                new LoginRequest("ana@test.com", "WrongPass1"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Me_ReturnsCurrentUser_AfterConfirm()
    {
        await _fx.CreateRegister().HandleAsync(
            new RegisterRequest("Ana", "ana@test.com", "Password1"),
            CancellationToken.None);

        var user = await _fx.Users.FindByEmailAsync("ana@test.com", CancellationToken.None);
        const string code = "654321";
        user!.BeginEmailConfirmation(
            EmailConfirmationService.HashCode(code),
            _fx.Clock.UtcNow.AddMinutes(15));
        await _fx.Users.SaveChangesAsync(CancellationToken.None);

        var confirmed = await _fx.CreateConfirmEmail().HandleAsync(
            new ConfirmEmailRequest("ana@test.com", code),
            CancellationToken.None);

        var me = await new GetCurrentUserHandler(
            _fx.Users,
            _fx.Profiles,
            _fx.Clock).HandleAsync(
            confirmed.UserId,
            CancellationToken.None);

        Assert.Equal("Ana", me.Name);
        Assert.True(me.IsActive);
    }

    [Fact]
    public async Task Login_LegacyHash_SucceedsAndRehashes()
    {
        const string legacy =
            "afPYtigqOhHQDj3ZVQC1sw==.funFjV1mernSMi4XjQPGZaLZtIJvOl3ms3pCpGuFNGE=";
        var user = User.CreateAdmin(
            "Administrador",
            "admin@cale.local",
            legacy,
            _fx.Clock.UtcNow);
        await _fx.Users.AddAsync(user, CancellationToken.None);
        await _fx.Users.SaveChangesAsync(CancellationToken.None);

        var login = _fx.CreateLogin();
        var result = await login.HandleAsync(
            new LoginRequest("admin@cale.local", "Admin123!"),
            CancellationToken.None);

        Assert.Equal(Roles.Admin, result.Role);
        var reloaded = await _fx.Users.FindByEmailAsync(
            "admin@cale.local",
            CancellationToken.None);
        Assert.False(_fx.Hasher.NeedsRehash(reloaded!.PasswordHash));
    }

    public void Dispose() => _fx.Dispose();
}
