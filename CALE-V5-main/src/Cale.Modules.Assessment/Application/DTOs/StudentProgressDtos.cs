namespace Cale.Modules.Assessment.Application.DTOs;

/// <summary>One finished attempt on the progress charts (chronological <see cref="Number"/>).</summary>
public sealed record ProgressAttemptDto(
    int Number,
    int AttemptId,
    string Mode,
    DateTime FinishedAt,
    decimal Score,
    int CorrectCount,
    int TotalQuestions,
    bool Passed,
    string Band,
    decimal PassThreshold,
    int? DurationSeconds);

/// <summary>Open = first result of the week, Close = last, High/Low = best/worst (Colombia weeks, Monday first).</summary>
public sealed record ProgressWeekDto(
    string WeekStart,
    string WeekEnd,
    int Count,
    decimal Open,
    decimal Close,
    decimal High,
    decimal Low,
    decimal Average);

public sealed record ProgressModeCountsDto(int All, int Exam, int Practice);

public sealed record StudentProgressDto(
    string Mode,
    int? Take,
    ProgressModeCountsDto ModeCounts,
    int TotalAttempts,
    int PassedAttempts,
    decimal? AverageScore,
    decimal? BestScore,
    decimal? LastScore,
    decimal? LastFiveAverage,
    int TrendWindow,
    decimal? RecentAverage,
    decimal? PreviousAverage,
    decimal? Trend,
    string TrendDirection,
    int? BestDurationSeconds,
    int? AverageDurationSeconds,
    int? LastDurationSeconds,
    int AttemptsWithoutDuration,
    decimal? ApprovalThreshold,
    int MaxIncorrectAnswers,
    IReadOnlyList<ProgressAttemptDto> Attempts,
    IReadOnlyList<ProgressWeekDto> Weeks);
