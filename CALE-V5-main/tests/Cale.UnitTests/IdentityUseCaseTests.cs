using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Cale.Modules.Identity.Domain;

namespace Cale.UnitTests;

public class IdentityUseCaseTests : IDisposable
{
    private readonly IdentityTestFixture _fx = new();

    [Fact]
    public async Task Register_AlwaysCreatesStudent()
    {
        var handler = new RegisterUserHandler(
            _fx.Users,
            _fx.Hasher,
            _fx.Tokens,
            _fx.Clock);

        var result = await handler.HandleAsync(
            new RegisterRequest("Ana", "ana@t.com", "Password1"),
            CancellationToken.None);

        Assert.Equal(Roles.Student, result.Role);
        Assert.Equal("ana@t.com", result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        var handler = new RegisterUserHandler(
            _fx.Users,
            _fx.Hasher,
            _fx.Tokens,
            _fx.Clock);
        var request = new RegisterRequest("Ana", "ana@t.com", "Password1");
        await handler.HandleAsync(request, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        await new RegisterUserHandler(
            _fx.Users,
            _fx.Hasher,
            _fx.Tokens,
            _fx.Clock).HandleAsync(
            new RegisterRequest("Ana", "ana@t.com", "Password1"),
            CancellationToken.None);

        var login = _fx.CreateLogin();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            login.HandleAsync(
                new LoginRequest("ana@t.com", "WrongPass1"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Login_Success_ReturnsNormalizedRole()
    {
        await new RegisterUserHandler(
            _fx.Users,
            _fx.Hasher,
            _fx.Tokens,
            _fx.Clock).HandleAsync(
            new RegisterRequest("Ana", "ana@t.com", "Password1"),
            CancellationToken.None);

        var login = _fx.CreateLogin();
        var result = await login.HandleAsync(
            new LoginRequest("ana@t.com", "Password1"),
            CancellationToken.None);

        Assert.Equal(Roles.Student, result.Role);
        Assert.Equal("Ana", result.Name);
    }

    [Fact]
    public async Task Me_ReturnsCurrentUser()
    {
        var registered = await new RegisterUserHandler(
            _fx.Users,
            _fx.Hasher,
            _fx.Tokens,
            _fx.Clock).HandleAsync(
            new RegisterRequest("Ana", "ana@t.com", "Password1"),
            CancellationToken.None);

        var me = await new GetCurrentUserHandler(
            _fx.Users,
            _fx.Profiles,
            _fx.Clock).HandleAsync(
            registered.UserId,
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
