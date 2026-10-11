using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Domain;
using Cale.Modules.LiveClassroom.Application.Commands;
using Cale.Modules.LiveClassroom.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Live;

/// <summary>
/// Ends live classes and game shows left open when the host closed the tab without pressing
/// "Terminar": no host panel/projector connected and no host or player action for
/// <see cref="AbandonAfter"/>. Uses the same end paths as the host, so clients get the normal
/// SessionEnded / GameEnded events.
/// </summary>
public sealed class AbandonedLiveRoomService : BackgroundService
{
    public static readonly TimeSpan AbandonAfter = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly LiveActivityTracker _tracker;
    private readonly IClock _clock;
    private readonly ILogger<AbandonedLiveRoomService> _logger;
    private readonly DateTime _bootedAt;

    public AbandonedLiveRoomService(
        IServiceScopeFactory scopes,
        LiveActivityTracker tracker,
        IClock clock,
        ILogger<AbandonedLiveRoomService> logger)
    {
        _scopes = scopes;
        _tracker = tracker;
        _clock = clock;
        _logger = logger;
        _bootedAt = clock.UtcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Abandoned live room sweep failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>Ends every open room idle for <see cref="AbandonAfter"/>. Returns how many were ended.</summary>
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var cutoff = now - AbandonAfter;
        if (_bootedAt > cutoff)
        {
            // Activity is kept in memory: right after a restart, give hosts time to reconnect.
            return 0;
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();

        var liveRooms = await db.Set<LiveSession>()
            .AsNoTracking()
            .Where(s => s.Status != LiveSessionStatuses.Ended && s.CreatedAt <= cutoff)
            .Select(s => new { s.Id, s.HostUserId })
            .ToListAsync(ct);
        var gameRooms = await db.Set<GameShowSession>()
            .AsNoTracking()
            .Where(s => s.Status != GameShowSessionStatuses.Ended && s.CreatedAt <= cutoff)
            .Select(s => new { s.Id, s.HostUserId })
            .ToListAsync(ct);

        var ended = 0;
        foreach (var room in liveRooms)
        {
            if (_tracker.IsActiveSince(LiveRoomKind.LiveClass, room.Id, cutoff))
            {
                continue;
            }

            try
            {
                using var roomScope = _scopes.CreateScope();
                var handler = roomScope.ServiceProvider.GetRequiredService<LiveSessionHandler>();
                await handler.ControlAsync(room.Id, "end", room.HostUserId, isAdmin: true, publicBaseUrl: "", ct);
                _tracker.Forget(LiveRoomKind.LiveClass, room.Id);
                ended++;
                _logger.LogInformation(
                    "Live class {SessionId} ended automatically after {Minutes} min without host activity",
                    room.Id,
                    AbandonAfter.TotalMinutes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not auto-end live class {SessionId}", room.Id);
            }
        }

        foreach (var room in gameRooms)
        {
            if (_tracker.IsActiveSince(LiveRoomKind.GameShow, room.Id, cutoff))
            {
                continue;
            }

            try
            {
                using var roomScope = _scopes.CreateScope();
                var handler = roomScope.ServiceProvider.GetRequiredService<GameShowHandler>();
                await handler.FinishAsync(room.Id, room.HostUserId, ct);
                _tracker.Forget(LiveRoomKind.GameShow, room.Id);
                ended++;
                _logger.LogInformation(
                    "Game show {SessionId} ended automatically after {Minutes} min without host activity",
                    room.Id,
                    AbandonAfter.TotalMinutes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not auto-end game show {SessionId}", room.Id);
            }
        }

        return ended;
    }
}
