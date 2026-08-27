using Cale.Modules.LiveClassroom.Application.Commands;
using Microsoft.AspNetCore.SignalR;

namespace Cale.Api.Hubs;

public sealed class LiveClassroomHub : Hub
{
    public const string GroupPrefix = "live-";

    private readonly LiveSessionHandler _handler;

    public LiveClassroomHub(LiveSessionHandler handler) => _handler = handler;

    public static string GroupName(int sessionId) => $"{GroupPrefix}{sessionId}";

    public async Task JoinAsHost(int sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
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
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
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
}
