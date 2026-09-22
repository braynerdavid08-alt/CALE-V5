using System.Security.Claims;
using Cale.BuildingBlocks.Domain.Auth;
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
    private readonly ILogger<GameShowHub> _logger;

    public GameShowHub(
        GameShowHandler handler,
        IGameShowStore store,
        ILogger<GameShowHub> logger)
    {
        _handler = handler;
        _store = store;
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

            await _handler.SetConnectionAsync(token, Context.ConnectionId, true, Context.ConnectionAborted);
            if (Context.ConnectionAborted.IsCancellationRequested)
            {
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
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
