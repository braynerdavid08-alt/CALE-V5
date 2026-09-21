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
    string Team);

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
    bool IsHostView);

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
