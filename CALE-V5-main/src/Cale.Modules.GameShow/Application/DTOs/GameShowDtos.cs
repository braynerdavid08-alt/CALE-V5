namespace Cale.Modules.GameShow.Application.DTOs;

public sealed record CreateGameShowRequest(
    string Title,
    string TeamAName,
    string TeamBName,
    IReadOnlyList<CreateGameShowRoundRequest> Rounds);

public sealed record CreateGameShowRoundRequest(
    string QuestionText,
    int? SourceQuestionId,
    IReadOnlyList<CreateGameShowAnswerRequest> Answers,
    string? Category = null,
    bool IsActive = true);

public sealed record CreateGameShowAnswerRequest(
    string Text,
    int Points,
    IReadOnlyList<string>? Aliases,
    bool IsActive = true);

public sealed record JoinGameShowRequest(
    string Code,
    string DisplayName,
    string? Team = null);

public sealed record AnswerGameShowRequest(string Text);

public sealed record AssignPlayerRequest(int PlayerId, string Team);

public sealed record ForceBuzzRequest(string Team);

public sealed record GameShowLobbyDto(
    int Id,
    string Title,
    string JoinCode,
    string Status,
    string TeamAName,
    string TeamBName,
    int TeamAScore,
    int TeamBScore,
    int CurrentRoundIndex,
    int RoundCount,
    IReadOnlyList<GameShowPlayerDto> Players,
    GameShowRoundPublicDto? CurrentRound,
    bool IsHostView,
    int? ViewerPlayerId = null,
    string? ViewerTeam = null,
    DateTime? LightningUntilUtc = null,
    bool IsLightning = false,
    int? SourcePackId = null,
    GameShowRoundChampionDto? RoundChampion = null,
    IReadOnlyList<GameShowPlayerStandingDto>? PlayerStandings = null,
    IReadOnlyList<GameShowPackLeaderboardEntryDto>? PackLeaderboard = null,
    GameShowSettingsDto? Settings = null);

public sealed record GameShowSettingsDto(
    int FaceOffSeconds,
    int ControlSeconds,
    int StealSeconds,
    int LightningSeconds,
    int RoundTransitionSeconds,
    int DrumrollMs,
    int RevealHighlightMs,
    int StrikeFlashMs,
    int CelebrationMs,
    int CorrectFlashMs,
    int ScoreboardFlashMs,
    int MaxStrikes,
    bool EnableFaceOff,
    bool EnableSteal,
    bool EnableLightning,
    bool EnableSounds,
    bool EnableAnimations,
    bool AllowPause,
    bool AllowSkipRound,
    bool AllowHostEndRound,
    bool EnableAudienceVote,
    string TieBreakMode,
    DateTime? UpdatedAt = null,
    int? UpdatedByUserId = null);

public sealed record GameShowPlayerDto(
    int Id,
    string DisplayName,
    string Team,
    bool IsConnected,
    int? UserId,
    string? AccentColor = null);

public sealed record GameShowRoundChampionDto(
    int PlayerId,
    string DisplayName,
    string Team,
    int CorrectAnswers);

public sealed record GameShowPlayerStandingDto(
    int PlayerId,
    string DisplayName,
    string Team,
    int CorrectAnswers,
    int StealsWon,
    int BuzzWins,
    string AccentColor);

public sealed record GameShowPackLeaderboardEntryDto(
    int SessionId,
    string Title,
    string TeamAName,
    string TeamBName,
    int TeamAScore,
    int TeamBScore,
    int CombinedScore,
    DateTime? EndedAt);

public sealed record GameShowRoundPublicDto(
    int Id,
    int SortOrder,
    string QuestionText,
    string Phase,
    string? ControllingTeam,
    string? BuzzWinnerTeam,
    int Strikes,
    int RoundPointsForController,
    bool StealSucceeded,
    DateTime? AnswerDeadlineUtc,
    IReadOnlyList<GameShowBoardAnswerPublicDto> Answers,
    int? ActivePlayerId = null,
    string? ActivePlayerName = null,
    string? ActivePlayerAccent = null,
    /// <summary>Whole seconds left until AnswerDeadlineUtc (server clock). Prefer this over client Date math.</summary>
    int? SecondsRemaining = null);

public sealed record GameShowBoardAnswerPublicDto(
    int Id,
    int Rank,
    string? Text,
    int? Points,
    bool IsRevealed);

public sealed record JoinGameShowResultDto(
    int SessionId,
    Guid PlayerToken,
    int PlayerId,
    string Team,
    GameShowLobbyDto Lobby);

public sealed record GameShowHistoryItemDto(
    int Id,
    string Title,
    string Status,
    string TeamAName,
    string TeamBName,
    int TeamAScore,
    int TeamBScore,
    DateTime CreatedAt,
    DateTime? EndedAt,
    int PlayerCount);

public sealed record UpsertGameShowPackRequest(
    string Name,
    string? Notes,
    IReadOnlyList<CreateGameShowRoundRequest> Rounds,
    string? DefaultTeamAName = null,
    string? DefaultTeamBName = null);

public sealed record CreateSessionFromPackRequest(
    string? Title,
    string? TeamAName,
    string? TeamBName);

public sealed record ReplayGameShowRequest(
    string? Title,
    string? TeamAName,
    string? TeamBName);

public sealed record GameShowPackSummaryDto(
    int Id,
    string Name,
    string? Notes,
    int RoundCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record GameShowPackDetailDto(
    int Id,
    string Name,
    string? Notes,
    int RoundCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CreateGameShowRequest Body);

public sealed record GameShowRoundStatDto(
    int RoundIndex,
    string QuestionText,
    int PointsAwarded,
    bool StealSucceeded,
    string? ControllingTeam,
    int Strikes,
    int CorrectAttempts,
    int WrongAttempts);

public sealed record GameShowStatsDto(
    int SessionId,
    string Title,
    string Status,
    string TeamAName,
    string TeamBName,
    int TeamAScore,
    int TeamBScore,
    string? WinnerTeam,
    int PlayerCount,
    int RoundCount,
    int CorrectAnswers,
    int WrongAnswers,
    int StealsSucceeded,
    int StealsFailed,
    IReadOnlyList<GameShowRoundStatDto> Rounds,
    DateTime CreatedAt,
    DateTime? EndedAt,
    IReadOnlyList<GameShowPlayerStandingDto>? Players = null,
    GameShowPlayerStandingDto? Mvp = null);
