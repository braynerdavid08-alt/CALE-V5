using System.Security.Claims;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.LiveClassroom.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Cale.Modules.LiveClassroom.Application.Commands;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Cale.Api.Hubs;

public sealed class LiveClassroomHub : Hub
{
    public const string GroupPrefix = "live-";

    private readonly LiveSessionHandler _handler;
    private readonly ILiveSessionStore _sessions;
    private readonly ILogger<LiveClassroomHub> _logger;

    public LiveClassroomHub(
        LiveSessionHandler handler,
        ILiveSessionStore sessions,
        ILogger<LiveClassroomHub> logger)
    {
        _handler = handler;
        _sessions = sessions;
        _logger = logger;
    }

    public static string GroupName(int sessionId) => $"{GroupPrefix}{sessionId}";

    public async Task JoinAsHost(int sessionId)
    {
        var userId = TryGetUserId()
            ?? throw new HubException("Debes iniciar sesión como anfitrión.");

        var session = await _sessions.GetByIdAsync(sessionId, Context.ConnectionAborted)
            ?? throw new HubException("Sesión no encontrada.");

        var isAdmin = Context.User?.IsInRole(Roles.Admin) == true;
        if (!isAdmin && session.HostUserId != userId)
        {
            throw new HubException("Solo el anfitrión puede controlar esta sesión.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
        _logger.LogInformation(
            "Live host connected {ConnectionId} session {SessionId} user {UserId}",
            Context.ConnectionId,
            sessionId,
            userId);
    }

    public async Task JoinAsParticipant(int sessionId, string participantToken)
    {
        if (!Guid.TryParse(participantToken, out var token))
        {
            throw new HubException("Token inválido.");
        }

        await _handler.SetConnectionAsync(
            token,
            Context.ConnectionId,
            connected: true,
            Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
        _logger.LogInformation(
            "Live participant connected {ConnectionId} session {SessionId}",
            Context.ConnectionId,
            sessionId);
    }

    public async Task SyncPresentationSlide(int sessionId, int slideIndex)
    {
        var userId = TryGetUserId()
            ?? throw new HubException("Debes iniciar sesión como anfitrión.");

        var session = await _sessions.GetByIdAsync(sessionId, Context.ConnectionAborted)
            ?? throw new HubException("Sesión no encontrada.");

        var isAdmin = Context.User?.IsInRole(Roles.Admin) == true;
        if (!isAdmin && session.HostUserId != userId)
        {
            throw new HubException("Solo el anfitrión puede controlar la presentación.");
        }

        if (slideIndex < 0)
        {
            slideIndex = 0;
        }

        var broadcaster = Context.GetHttpContext()?.RequestServices
            .GetRequiredService<ILiveSessionBroadcaster>();
        if (broadcaster is null)
        {
            return;
        }

        await broadcaster.PresentationSlideChangedAsync(
            sessionId,
            new { slideIndex },
            Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            await _handler.DisconnectByConnectionAsync(
                Context.ConnectionId,
                Context.ConnectionAborted);
            if (exception is not null)
            {
                _logger.LogWarning(
                    exception,
                    "Live hub disconnected with error {ConnectionId}",
                    Context.ConnectionId);
            }
            else
            {
                _logger.LogInformation("Live hub disconnected {ConnectionId}", Context.ConnectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear live participant on disconnect");
        }

        await base.OnDisconnectedAsync(exception);
    }

    private int? TryGetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}

public sealed class LiveSessionBroadcaster : Cale.Modules.LiveClassroom.Application.Abstractions.ILiveSessionBroadcaster
{
    private readonly IHubContext<LiveClassroomHub> _hub;

    public LiveSessionBroadcaster(IHubContext<LiveClassroomHub> hub) => _hub = hub;

    public Task LobbyUpdatedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("LobbyUpdated", payload, ct);

    public Task QuestionStartedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("QuestionStarted", payload, ct);

    public Task QuestionClosedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("QuestionClosed", payload, ct);

    public Task AnswerReceivedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("AnswerReceived", payload, ct);

    public Task SessionEndedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("SessionEnded", payload, ct);

    public Task RevealUpdatedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("RevealUpdated", payload, ct);

    public Task RankingUpdatedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("RankingUpdated", payload, ct);

    public Task DoubtsUpdatedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("DoubtsUpdated", payload, ct);

    public Task RematchReadyAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("RematchReady", payload, ct);

    public Task SurpriseQueuedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("SurpriseQueued", payload, ct);

    public Task PresentationSlideChangedAsync(int sessionId, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group(LiveClassroomHub.GroupName(sessionId))
            .SendAsync("PresentationSlideChanged", payload, ct);
}
