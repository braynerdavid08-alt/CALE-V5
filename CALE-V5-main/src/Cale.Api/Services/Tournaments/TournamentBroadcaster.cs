using Cale.Api.Hubs;
using Cale.Api.Services.Play;
using Microsoft.AspNetCore.SignalR;

namespace Cale.Api.Services.Tournaments;

public sealed class TournamentBroadcaster : ITournamentBroadcaster
{
    private readonly IHubContext<TournamentHub> _hub;

    public TournamentBroadcaster(IHubContext<TournamentHub> hub) => _hub = hub;

    private IClientProxy Group(int tournamentId) =>
        _hub.Clients.Group(TournamentHub.GroupName(tournamentId));

    public Task ParticipantJoined(int tournamentId, TournamentParticipantDto participant, CancellationToken ct) =>
        Group(tournamentId).SendAsync("ParticipantJoined", participant, ct);

    public Task ParticipantDisconnected(int tournamentId, int userId, CancellationToken ct) =>
        Group(tournamentId).SendAsync("ParticipantDisconnected", new { userId }, ct);

    public Task ParticipantReconnected(int tournamentId, int userId, CancellationToken ct) =>
        Group(tournamentId).SendAsync("ParticipantReconnected", new { userId }, ct);

    public Task LobbyUpdated(int tournamentId, TournamentLobbyDto lobby, CancellationToken ct) =>
        Group(tournamentId).SendAsync("LobbyUpdated", lobby, ct);

    public Task TournamentStarted(int tournamentId, TournamentBracketDto bracket, CancellationToken ct) =>
        Group(tournamentId).SendAsync("TournamentStarted", bracket, ct);

    public Task MatchAssigned(int tournamentId, int userId, TournamentMatchViewDto match, CancellationToken ct) =>
        _hub.Clients.Group(TournamentHub.UserGroup(userId)).SendAsync("MatchAssigned", match, ct);

    public Task MatchUpdated(int tournamentId, TournamentMatchViewDto match, CancellationToken ct) =>
        Group(tournamentId).SendAsync("MatchUpdated", match, ct);

    public Task MatchFinished(int tournamentId, TournamentBracketMatchDto match, CancellationToken ct) =>
        Group(tournamentId).SendAsync("MatchFinished", match, ct);

    public Task RoundStarted(int tournamentId, int roundNumber, string roundName, CancellationToken ct) =>
        Group(tournamentId).SendAsync("RoundStarted", new { roundNumber, roundName }, ct);

    public Task ChampionDeclared(int tournamentId, int championUserId, string championName, CancellationToken ct) =>
        Group(tournamentId).SendAsync("ChampionDeclared", new { championUserId, championName }, ct);

    public Task TournamentFinished(int tournamentId, CancellationToken ct) =>
        Group(tournamentId).SendAsync("TournamentFinished", new { tournamentId }, ct);

    public Task AchievementUnlocked(int userId, IReadOnlyList<BadgeDto> badges, CancellationToken ct) =>
        _hub.Clients.Group(TournamentHub.UserGroup(userId)).SendAsync("AchievementUnlocked", badges, ct);
}
