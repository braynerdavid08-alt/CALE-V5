using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;
using Xunit;

namespace Cale.UnitTests;

public sealed class AccountSuspensionTests : IDisposable
{
    private const int AdminId = 999;
    private readonly IdentityTestFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private async Task<int> RegisterAnaAsync()
    {
        var registered = await _fx.CreateRegister().HandleAsync(
            new RegisterRequest("Ana", "ana@test.com", "Password1"),
            CancellationToken.None);
        return registered.UserId!.Value;
    }

    [Fact]
    public async Task Suspending_without_a_reason_is_rejected_and_the_account_stays_active()
    {
        var userId = await RegisterAnaAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _fx.CreateSetActive().HandleAsync(
                AdminId, userId, new SetUserActiveRequest(false, "corto"), CancellationToken.None));

        Assert.Equal("suspension_reason_required", error.ErrorCode);
        var user = await _fx.Users.GetByIdAsync(userId, CancellationToken.None);
        Assert.True(user!.IsActive);
        Assert.Empty(await _fx.AccountStatus.ListAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task Suspended_user_sees_the_reason_and_end_date_when_signing_in()
    {
        var userId = await RegisterAnaAsync();
        await _fx.CreateSetActive().HandleAsync(
            AdminId,
            userId,
            new SetUserActiveRequest(false, "Compartió preguntas del examen en redes.", "Captura del 3 de octubre", 7),
            CancellationToken.None);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _fx.CreateLogin().HandleAsync(new LoginRequest("ana@test.com", "Password1"), CancellationToken.None));

        Assert.Equal("account_suspended", error.ErrorCode);
        Assert.Contains("Compartió preguntas del examen en redes.", error.Message);
        Assert.Contains("termina el 20/08/2026", error.Message);

        var history = await _fx.CreateSetActive().HistoryAsync(userId, CancellationToken.None);
        var entry = Assert.Single(history);
        Assert.Equal(AccountStatusActions.Suspended, entry.Action);
        Assert.Equal(AdminId, entry.ActorUserId);
        Assert.Equal("Captura del 3 de octubre", entry.Evidence);
        Assert.Equal(_fx.Clock.UtcNow.AddDays(7), entry.SuspendedUntil);
    }

    [Fact]
    public async Task Temporary_suspension_ends_by_itself_and_is_recorded()
    {
        var userId = await RegisterAnaAsync();
        await _fx.CreateSetActive().HandleAsync(
            AdminId,
            userId,
            new SetUserActiveRequest(false, "Lenguaje ofensivo en el aula en vivo.", null, 3),
            CancellationToken.None);

        _fx.Clock.Advance(TimeSpan.FromDays(3));
        var login = await _fx.CreateLogin().HandleAsync(
            new LoginRequest("ana@test.com", "Password1"), CancellationToken.None);

        Assert.Equal("Ana", login.Name);
        var history = await _fx.AccountStatus.ListAsync(userId, CancellationToken.None);
        Assert.Equal(2, history.Count);
        Assert.Equal(AccountStatusActions.Reactivated, history[0].Action);
        Assert.Null(history[0].ActorUserId);
    }

    [Fact]
    public async Task Admin_review_reactivates_with_a_note_and_keeps_the_history()
    {
        var userId = await RegisterAnaAsync();
        var handler = _fx.CreateSetActive();
        await handler.HandleAsync(
            AdminId, userId, new SetUserActiveRequest(false, "Suplantación de identidad reportada."), CancellationToken.None);

        _fx.Clock.Advance(TimeSpan.FromHours(2));
        var updated = await handler.HandleAsync(
            AdminId, userId, new SetUserActiveRequest(true, "Revisado: el reporte era falso."), CancellationToken.None);

        Assert.True(updated.IsActive);
        var history = await handler.HistoryAsync(userId, CancellationToken.None);
        Assert.Equal(
            [AccountStatusActions.Reactivated, AccountStatusActions.Suspended],
            history.Select(h => h.Action).ToArray());
        Assert.Equal("Revisado: el reporte era falso.", history[0].Reason);
        Assert.Null(history[1].SuspendedUntil);
    }
}
