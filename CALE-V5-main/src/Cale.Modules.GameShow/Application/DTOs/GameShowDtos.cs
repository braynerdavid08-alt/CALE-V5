namespace Cale.Modules.GameShow.Application.DTOs;

public sealed record CreateGameShowRequest(
    string Title,
    string TeamAName,
    string TeamBName,
    IReadOnlyList<CreateGameShowRoundRequest> Rounds);

public sealed record CreateGameShowRoundRequest(
    string QuestionText,
    int? SourceQuestionId,
    IReadOnlyList<CreateGameShowAnswerRequest> Answers);

public sealed record CreateGameShowAnswerRequest(
    string Text,
    int Points,
    IReadOnlyList<string>? Aliases);

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
    string? ViewerTeam = null);

public sealed record GameShowPlayerDto(
    int Id,
    string DisplayName,
    string Team,
    bool IsConnected,
    int? UserId);

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
    IReadOnlyList<GameShowBoardAnswerPublicDto> Answers);

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
    DateTime? EndedAt);
