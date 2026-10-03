namespace Cale.Modules.Assessment.Application.Abstractions;

public sealed record AttemptResultRow(
    int Id,
    int UserId,
    string Mode,
    decimal Percent,
    bool Passed,
    int TimeSeconds,
    DateTime StartedAt,
    DateTime FinishedAt);
