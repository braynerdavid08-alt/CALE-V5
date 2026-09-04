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
    bool CaleStandardPreset = false,
    IReadOnlyList<int>? BankIds = null,
    IReadOnlyList<string>? TopicFilters = null,
    Dictionary<int, List<string>>? BankTopicFilters = null,
    Dictionary<int, int>? BankQuestionQuotas = null,
    IReadOnlyList<string>? DifficultyFilters = null,
    int? PresentationId = null);

public sealed record CreateLiveSessionRequest(
    string? Title,
    string Mode,
    int? BankId,
    IReadOnlyList<int>? BankIds,
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
    bool RevealCorrect,
    bool IsSurprise = false);

public sealed record LiveParticipantDto(
    int Id,
    string DisplayName,
    bool IsConnected,
    int? UserId);

public sealed record LiveRankEntryDto(
    int Rank,
    int ParticipantId,
    string DisplayName,
    int Score,
    int CorrectCount,
    int AnswerCount);

public sealed record LiveRankingDto(
    IReadOnlyList<LiveRankEntryDto> Top,
    int? MyParticipantId,
    int? MyRank,
    int? MyScore);

public sealed record LiveAnswerRosterEntryDto(
    int ParticipantId,
    string DisplayName,
    bool Answered,
    bool? IsCorrect,
    int? OptionId);

public sealed record LiveAnswerRosterDto(
    int SessionQuestionId,
    int QuestionIndex,
    bool RevealCorrectness,
    int CorrectCount,
    int IncorrectCount,
    int UnansweredCount,
    IReadOnlyList<LiveAnswerRosterEntryDto> Correct,
    IReadOnlyList<LiveAnswerRosterEntryDto> Incorrect,
    IReadOnlyList<LiveAnswerRosterEntryDto> Unanswered);

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
    string JoinUrl,
    LiveRankingDto? Ranking = null);

public sealed record JoinLiveSessionResponse(
    int SessionId,
    Guid ParticipantToken,
    int ParticipantId,
    string DisplayName,
    string Title,
    string Status,
    string JoinCode);

public sealed record LiveHostControlRequest(
    string Action,
    LiveQuickQuestionRequest? QuickQuestion = null);

public sealed record LiveQuickQuestionRequest(
    string Text,
    IReadOnlyList<LiveQuickOptionRequest> Options,
    string? Explanation = null,
    string? Topic = null);

public sealed record LiveQuickOptionRequest(
    string Text,
    bool IsCorrect);

public sealed record LiveDoubtRequest(Guid ParticipantToken, string Text);

public sealed record LiveDoubtVoteRequest(Guid ParticipantToken);

public sealed record LiveDoubtDto(
    int Id,
    int ParticipantId,
    string AuthorName,
    string Text,
    int VoteCount,
    bool IsResolved,
    bool VotedByMe,
    DateTime CreatedAt);

public sealed record LiveTopicStatDto(
    string Topic,
    int Answered,
    int Correct,
    double AccuracyPercent);

public sealed record LiveQuestionStatDto(
    int Index,
    int SessionQuestionId,
    string Text,
    string? Topic,
    int Answered,
    int Correct,
    double AccuracyPercent,
    bool IsSurprise);

public sealed record LiveAnalyticsDto(
    int SessionId,
    string Title,
    string Mode,
    int ParticipantCount,
    int QuestionCount,
    int TotalAnswers,
    int CorrectAnswers,
    double OverallAccuracyPercent,
    IReadOnlyList<LiveQuestionStatDto> Questions,
    IReadOnlyList<LiveTopicStatDto> Topics,
    IReadOnlyList<string> Recommendations,
    LiveRankingDto Ranking);

public sealed record LiveRematchResponse(
    int NewSessionId,
    string JoinCode,
    string JoinUrl,
    LiveLobbyDto Lobby);

public sealed record LivePresentationSlideDto(
    int Position,
    string Title,
    string BackgroundJson,
    string ElementsJson);

public sealed record LivePresentationDto(
    int PresentationId,
    string Title,
    int SlideIndex,
    int SlideCount,
    IReadOnlyList<LivePresentationSlideDto> Slides);
