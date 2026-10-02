namespace Cale.Modules.Assessment.Application.Abstractions;

/// <summary>Fields of a finished attempt needed by the progress dashboard.</summary>
public sealed record AttemptProgressRow(
    int Id,
    string Mode,
    int TotalQuestions,
    int CorrectCount,
    decimal Percent,
    bool Passed,
    int TimeSeconds,
    DateTime StartedAt,
    DateTime FinishedAt);
