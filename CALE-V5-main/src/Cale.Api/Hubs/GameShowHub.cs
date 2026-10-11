using System.Security.Claims;
using Cale.Api.Services.Live;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Application.Abstractions;
using Cale.Modules.GameShow.Application.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Cale.Api.Hubs;

public sealed class GameShowHub : Hub
{
    public const string GroupPrefix = "game-show-";

    private readonly GameShowHandler _handler;
    private readonly IGameShowStore _store;
    private readonly LiveActivityTracker _activity;
    private readonly IClock _clock;
    private readonly ILogger<GameShowHub> _logger;

    public GameShowHub(
        GameShowHandler handler,
        IGameShowStore store,
        LiveActivityTracker activity,
        IClock clock,
        ILogger<GameShowHub> logger)
    {
        _handler = handler;
        _store = store;
        _activity = activity;
        _clock = clock;
        _logger = logger;
    }

    public static string GroupName(int sessionId) => $"{GroupPrefix}{sessionId}";

    public async Task JoinAsPlayer(int sessionId, string playerToken)
    {
        if (!Guid.TryParse(playerToken, out var token))
        {
            throw new HubException("Token inválido.");
        }

        try
        {
            var player = await _store.GetPlayerByTokenAsync(token, Context.ConnectionAborted)
                ?? throw new HubException("Jugador no encontrado.");
            if (player.SessionId != sessionId)
            {
                throw new HubException("Token inválido para esta partida.");
            }

            var previousConnectionId = player.ConnectionId;
            await _handler.SetConnectionAsync(token, Context.ConnectionId, true, Context.ConnectionAborted);
            if (Context.ConnectionAborted.IsCancellationRequested)
            {
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
            if (!string.IsNullOrEmpty(previousConnectionId) && previousConnectionId != Context.ConnectionId)
            {
                // Newest tab wins: the old one stops receiving the game and is told why.
                await Groups.RemoveFromGroupAsync(previousConnectionId, GroupName(sessionId));
                await Clients.Client(previousConnectionId).SendAsync(
                    LiveClassroomHub.ReplacedByAnotherTabEvent,
                    Context.ConnectionAborted);
            }
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            _logger.LogDebug(
                "JoinAsPlayer cancelled for session {SessionId} (client disconnected)",
                sessionId);
        }
    }

    public async Task JoinAsScreen(int sessionId)
    {
        try
        {
            var session = await _store.GetByIdAsync(sessionId, Context.ConnectionAborted)
                ?? throw new HubException("Partida no encontrada.");
            _ = session;
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
            _activity.HostConnected(LiveRoomKind.GameShow, sessionId, Context.ConnectionId, _clock.UtcNow);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            _logger.LogDebug(
                "JoinAsScreen cancelled for session {SessionId} (client disconnected)",
                sessionId);
        }
    }

    public async Task JoinAsHost(int sessionId)
    {
        try
        {
            var userId = TryGetUserId()
                ?? throw new HubException("Debes iniciar sesión.");
            var session = await _store.GetByIdAsync(sessionId, Context.ConnectionAborted)
                ?? throw new HubException("Partida no encontrada.");
            var isAdmin = Context.User?.IsInRole(Roles.Admin) == true;
            if (!isAdmin && session.HostUserId != userId)
            {
                throw new HubException("Solo el anfitrión puede controlar esta partida.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
            _activity.HostConnected(LiveRoomKind.GameShow, sessionId, Context.ConnectionId, _clock.UtcNow);
            _logger.LogInformation("GameShow host {UserId} joined session {SessionId}", userId, sessionId);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            _logger.LogDebug(
                "JoinAsHost cancelled for session {SessionId} (client disconnected)",
                sessionId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _activity.ConnectionClosed(Context.ConnectionId, _clock.UtcNow);
        try
        {
            var player = await _store.GetPlayerByConnectionIdAsync(
                Context.ConnectionId,
                CancellationToken.None);
            if (player is not null)
            {
                await _handler.SetConnectionAsync(
                    player.PlayerToken,
                    null,
                    false,
                    CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GameShow disconnect cleanup failed");
        }

        await base.OnDisconnectedAsync(exception);
    }

    private int? TryGetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}

public sealed class GameShowBroadcaster : IGameShowBroadcaster
{
    private readonly IHubContext<GameShowHub> _hub;

    public GameShowBroadcaster(IHubContext<GameShowHub> hub) => _hub = hub;

    public Task LobbyUpdatedAsync(int sessionId, GameShowLobbyDto lobby, CancellationToken ct) =>
        _hub.Clients.Group(GameShowHub.GroupName(sessionId))
            .SendAsync("LobbyUpdated", lobby, ct);

    public Task EventAsync(int sessionId, string eventName, object payload, CancellationToken ct) =>
        _hub.Clients.Group(GameShowHub.GroupName(sessionId))
            .SendAsync(eventName, payload, ct);
}
