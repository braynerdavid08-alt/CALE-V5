using Cale.Api.Services.Play;

namespace Cale.Api.Services.Tournaments;

public sealed record CreateTournamentRequest(
    string Name,
    string? Description,
    DateTime ScheduledAt,
    int Capacity,
    int SecondsPerQuestion,
    string QuestionMode,
    IReadOnlyList<string>? Categories,
    IReadOnlyList<int>? ManualQuestionIds,
    IReadOnlyList<int>? BankIds = null);

public sealed record TournamentQuestionBankDto(int Id, string Name, string Kind);

public sealed record TournamentListItemDto(
    int Id,
    string Name,
    string? Description,
    DateTime ScheduledAt,
    string Status,
    int Capacity,
    int RegisteredCount,
    int ConnectedCount,
    string OrganizerName,
    bool AmIRegistered,
    int? MyMatchId);

public sealed record TournamentParticipantDto(
    int UserId,
    string Name,
    bool IsConnected,
    bool IsEliminated,
    int? BracketSlot,
    string? PhotoUrl = null);

public sealed record TournamentLobbyDto(
    int Id,
    string Name,
    string? Description,
    DateTime ScheduledAt,
    string Status,
    int Capacity,
    int BracketSize,
    int CurrentRoundNumber,
    string OrganizerName,
    int OrganizerUserId,
    IReadOnlyList<TournamentParticipantDto> Participants,
    bool AmIRegistered,
    bool CanJoin,
    int? MyMatchId,
    int? ChampionUserId,
    string? ChampionName,
    string? ChampionPhotoUrl = null);

public sealed record TournamentBracketMatchDto(
    int Id,
    int RoundNumber,
    string RoundName,
    int SlotIndex,
    int? Player1UserId,
    string? Player1Name,
    int? Player2UserId,
    string? Player2Name,
    bool Player1IsBye,
    bool Player2IsBye,
    string Status,
    int? WinnerUserId,
    string? FinishReason,
    int Player1Correct,
    int Player2Correct,
    string? Player1PhotoUrl = null,
    string? Player2PhotoUrl = null);

public sealed record TournamentBracketDto(
    int TournamentId,
    string Status,
    int BracketSize,
    int CurrentRoundNumber,
    IReadOnlyList<TournamentBracketMatchDto> Matches,
    int? ChampionUserId,
    string? ChampionName,
    string? ChampionPhotoUrl = null);

public sealed record TournamentMatchViewDto(
    int MatchId,
    int TournamentId,
    string TournamentName,
    int RoundNumber,
    string RoundName,
    string Status,
    int? Player1UserId,
    string Player1Name,
    int? Player2UserId,
    string Player2Name,
    int Player1Correct,
    int Player2Correct,
    int Player1TimeMs,
    int Player2TimeMs,
    int CurrentQuestionIndex,
    int? CurrentMatchQuestionId,
    int TotalQuestions,
    int SecondsPerQuestion,
    DateTime? QuestionStartedAt,
    int? SecondsLeft,
    PlayQuestionDto? CurrentQuestion,
    bool AmIPlayer,
    bool WaitingOpponent,
    int? WinnerUserId,
    string? FinishReason,
    IReadOnlyList<AnsweredQuestionDto> MyAnswers,
    string? Player1PhotoUrl = null,
    string? Player2PhotoUrl = null);

public sealed record TournamentAnswerRequest(int MatchQuestionId, int? OptionId);

public sealed record TournamentAnswerResultDto(
    bool Correct,
    int? CorrectOptionId,
    string? Explanation,
    TournamentMatchViewDto State,
    IReadOnlyList<BadgeDto> NewBadges);

public sealed record TournamentTrophyDto(
    int Id,
    int TournamentId,
    string TournamentName,
    DateTime ObtainedAt,
    string ChampionResult);

public sealed record CompetitiveProfileDto(
    LevelDto Level,
    int? GeneralRank,
    int TrophyCount,
    int AchievementCount,
    int TournamentsWon,
    int TournamentsPlayed,
    int MatchWins,
    int MatchLosses,
    int FinalsReached,
    IReadOnlyList<TournamentTrophyDto> Trophies,
    IReadOnlyList<BadgeDto> Badges);
