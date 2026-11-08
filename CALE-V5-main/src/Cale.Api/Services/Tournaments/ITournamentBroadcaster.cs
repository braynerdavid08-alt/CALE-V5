namespace Cale.Api.Services.Tournaments;

public interface ITournamentBroadcaster
{
    Task ParticipantJoined(int tournamentId, TournamentParticipantDto participant, CancellationToken ct);
    Task ParticipantDisconnected(int tournamentId, int userId, CancellationToken ct);
    Task ParticipantReconnected(int tournamentId, int userId, CancellationToken ct);
    Task LobbyUpdated(int tournamentId, TournamentLobbyDto lobby, CancellationToken ct);
    Task TournamentStarted(int tournamentId, TournamentBracketDto bracket, CancellationToken ct);
    Task MatchAssigned(int tournamentId, int userId, TournamentMatchViewDto match, CancellationToken ct);

    /// <summary>Tournament-group cue so lobby clients can navigate even if the per-user MatchAssigned was missed.</summary>
    Task MatchReady(int tournamentId, int userId, int matchId, CancellationToken ct);
    Task MatchUpdated(int tournamentId, TournamentMatchViewDto match, CancellationToken ct);
    Task MatchFinished(int tournamentId, TournamentBracketMatchDto match, CancellationToken ct);
    Task RoundStarted(int tournamentId, int roundNumber, string roundName, CancellationToken ct);
    Task ChampionDeclared(int tournamentId, int championUserId, string championName, CancellationToken ct);
    Task TournamentFinished(int tournamentId, CancellationToken ct);
    Task AchievementUnlocked(int userId, IReadOnlyList<Play.BadgeDto> badges, CancellationToken ct);
}
