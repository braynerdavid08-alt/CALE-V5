namespace Cale.Modules.LiveClassroom.Application.DTOs;

public sealed record LiveSessionConfigDto(
    int QuestionCount = 10,
    int SecondsPerQuestion = 30,
    bool Randomize = true,
    bool ShuffleOptions = true,
    bool ShowRanking = false,
    bool AnonymousNames = false,
    string FeedbackTiming = "end",
    string? TopicFilter = null,
    string? DifficultyFilter = null,
    bool CaleStandardPreset = false);

public sealed record CreateLiveSessionRequest(
    string? Title,
    string Mode,
    int? BankId,
    LiveSessionConfigDto? Config);

public sealed record JoinLiveSessionRequest(
    string Code,
    string DisplayName);

public sealed record LiveAnswerRequest(
    Guid ParticipantToken,
    int OptionId);

public sealed record LiveOptionDto(
    int Id,
    string Text,
    string? ImageUrl,
    bool? IsCorrect);

public sealed record LiveQuestionPayloadDto(
    int SessionQuestionId,
    int QuestionId,
    int Index,
    int Total,
    string Text,
    string? ImageUrl,
    string? Topic,
    string? Explanation,
    IReadOnlyList<LiveOptionDto> Options,
    DateTime? OpensAt,
    DateTime? ClosesAt,
    int SecondsPerQuestion,
    bool RevealCorrect);

public sealed record LiveParticipantDto(
    int Id,
    string DisplayName,
    bool IsConnected,
    int? UserId);

public sealed record LiveLobbyDto(
    int SessionId,
    string Title,
    string JoinCode,
    string Status,
    string Mode,
    int BankId,
    int ParticipantCount,
    int ConnectedCount,
    IReadOnlyList<LiveParticipantDto> Participants,
    LiveSessionConfigDto Config,
    int QuestionCount,
    int CurrentQuestionIndex,
    bool RevealCorrect,
    LiveQuestionPayloadDto? CurrentQuestion,
    int AnswersReceived,
    string JoinUrl);

public sealed record JoinLiveSessionResponse(
    int SessionId,
    Guid ParticipantToken,
    int ParticipantId,
    string DisplayName,
    string Title,
    string Status,
    string JoinCode);

public sealed record LiveHostControlRequest(string Action);
