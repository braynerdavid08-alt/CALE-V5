namespace Cale.Modules.Assessment.Application.DTOs;

public sealed record StartExamRequest(
    int? BankId,
    int? ExamId,
    int QuestionCount,
    string Mode,
    int TimeMinutes);

public sealed record TakeOptionDto(int Id, string Text, string? ImageUrl);

public sealed record TakeQuestionDto(
    int Id,
    int Order,
    string Text,
    string Type,
    string? ImageUrl,
    IReadOnlyList<TakeOptionDto> Options);

public sealed record StartExamResponse(
    int AttemptId,
    DateTime StartedAt,
    DateTime? ExpiresAt,
    int TimeMinutes,
    IReadOnlyList<TakeQuestionDto> Questions);

public sealed record AnswerRequest(int QuestionId, int OptionId);

public sealed record FinishResponse(
    int AttemptId,
    int? ExamId,
    int TotalQuestions,
    int CorrectCount,
    decimal Percent,
    bool Passed,
    int TimeSeconds,
    IReadOnlyList<ScoreBreakdownDto> ByTopic,
    IReadOnlyList<ScoreBreakdownDto> ByBlock,
    decimal? BestPercent);

public sealed record ScoreBreakdownDto(
    string Label,
    int CorrectCount,
    int TotalQuestions,
    decimal Percent);

public sealed record ReviewOptionDto(
    int Id,
    string Text,
    bool IsCorrect,
    bool Selected,
    string? ImageUrl);

public sealed record ReviewQuestionDto(
    int Id,
    int Order,
    string Text,
    string Type,
    string? ImageUrl,
    string? Explanation,
    bool IsCorrect,
    IReadOnlyList<ReviewOptionDto> Options);

public sealed record ReviewResponse(
    FinishResponse Result,
    IReadOnlyList<ReviewQuestionDto> Questions);

public sealed record SaveRatingRequest(int AttemptId, int Stars, string? Comment);

public sealed record ManageRatingRequest(
    bool? Reviewed,
    bool? Hidden,
    string? Critique);

public sealed record RatingDto(
    int Id,
    int UserId,
    string UserName,
    int? AttemptId,
    int Stars,
    string? Comment,
    bool Reviewed,
    bool Hidden,
    DateTime CreatedAt);

public sealed record ResultRowDto(
    int AttemptId,
    int UserId,
    string UserName,
    string Mode,
    decimal Percent,
    bool Passed,
    DateTime StartedAt,
    DateTime? FinishedAt);
