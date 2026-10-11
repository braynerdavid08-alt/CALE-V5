using Cale.Api.Middleware;
using Cale.Api.Services.Live;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.GameShow.Domain;
using Cale.Modules.LiveClassroom.Domain;
using Cale.UnitTests.Fakes;
using Cale.UnitTests.Security.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cale.UnitTests.Live;

public sealed class AbandonedLiveRoomTests : IAsyncLifetime
{
    private readonly SecurityApiFactory _factory = new();
    private readonly LiveActivityTracker _tracker = new();
    private FakeClock _clock = null!;

    public async Task InitializeAsync()
    {
        _ = _factory.Server;
        await _factory.SeedAsync();
        _clock = new FakeClock(DateTime.UtcNow);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Rooms_without_host_activity_for_30_minutes_are_ended()
    {
        var liveId = await AddLiveSessionAsync();
        var gameId = await AddGameShowAsync();
        var service = NewService();
        _clock.Advance(AbandonedLiveRoomService.AbandonAfter + TimeSpan.FromMinutes(1));

        var ended = await service.SweepAsync(CancellationToken.None);

        Assert.Equal(2, ended);
        Assert.Equal(LiveSessionStatuses.Ended, await LiveStatusAsync(liveId));
        Assert.Equal(GameShowSessionStatuses.Ended, await GameStatusAsync(gameId));
    }

    [Fact]
    public async Task Connected_host_or_recent_activity_keeps_the_room_open()
    {
        var connectedLive = await AddLiveSessionAsync();
        var touchedGame = await AddGameShowAsync();
        var service = NewService();
        _tracker.HostConnected(LiveRoomKind.LiveClass, connectedLive, "host-conn", _clock.UtcNow);
        _clock.Advance(AbandonedLiveRoomService.AbandonAfter + TimeSpan.FromMinutes(1));
        _tracker.Touch(LiveRoomKind.GameShow, touchedGame, _clock.UtcNow.AddMinutes(-10));

        var ended = await service.SweepAsync(CancellationToken.None);

        Assert.Equal(0, ended);
        Assert.NotEqual(LiveSessionStatuses.Ended, await LiveStatusAsync(connectedLive));
        Assert.NotEqual(GameShowSessionStatuses.Ended, await GameStatusAsync(touchedGame));
    }

    [Fact]
    public async Task Host_leaving_starts_the_30_minute_countdown()
    {
        var liveId = await AddLiveSessionAsync();
        var service = NewService();
        _tracker.HostConnected(LiveRoomKind.LiveClass, liveId, "host-conn", _clock.UtcNow);
        _clock.Advance(TimeSpan.FromHours(2));
        _tracker.ConnectionClosed("host-conn", _clock.UtcNow);

        _clock.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(0, await service.SweepAsync(CancellationToken.None));

        _clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(1, await service.SweepAsync(CancellationToken.None));
        Assert.Equal(LiveSessionStatuses.Ended, await LiveStatusAsync(liveId));
    }

    [Fact]
    public async Task Nothing_is_ended_right_after_a_restart()
    {
        await AddLiveSessionAsync();
        var service = NewService();
        _clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(0, await service.SweepAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("/api/live/sessions/42/control", true, LiveRoomKind.LiveClass, 42)]
    [InlineData("/api/live/sessions/7/questions/3/answer", true, LiveRoomKind.LiveClass, 7)]
    [InlineData("/api/game-show/15/buzz", true, LiveRoomKind.GameShow, 15)]
    [InlineData("/api/game-show/packs/3", false, LiveRoomKind.LiveClass, 0)]
    [InlineData("/api/live/sessions/join", false, LiveRoomKind.LiveClass, 0)]
    [InlineData("/api/auth/me", false, LiveRoomKind.LiveClass, 0)]
    public void Activity_routes_are_recognised(string path, bool matches, LiveRoomKind kind, int id)
    {
        var ok = LiveActivityMiddleware.TryMatch(path, out var foundKind, out var foundId);

        Assert.Equal(matches, ok);
        if (matches)
        {
            Assert.Equal(kind, foundKind);
            Assert.Equal(id, foundId);
        }
    }

    private AbandonedLiveRoomService NewService() =>
        new(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            _tracker,
            _clock,
            NullLogger<AbandonedLiveRoomService>.Instance);

    private async Task<int> AddLiveSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var session = LiveSession.Create(
            _factory.Users.Teacher1.Id,
            "Clase olvidada",
            "AB" + Random.Shared.Next(1000, 9999),
            LiveSessionModes.Exam,
            _factory.Data.PrivateBankId,
            "{}",
            _clock.UtcNow.AddHours(-3));
        db.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<int> AddGameShowAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var session = new GameShowSession
        {
            HostUserId = _factory.Users.Teacher1.Id,
            Title = "Concurso olvidado",
            JoinCode = "GS" + Random.Shared.Next(1000, 9999),
            Status = GameShowSessionStatuses.Running,
            CreatedAt = _clock.UtcNow.AddHours(-3)
        };
        db.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<string> LiveStatusAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        return await db.Set<LiveSession>().Where(s => s.Id == id).Select(s => s.Status).SingleAsync();
    }

    private async Task<string> GameStatusAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        return await db.Set<GameShowSession>().Where(s => s.Id == id).Select(s => s.Status).SingleAsync();
    }
}
