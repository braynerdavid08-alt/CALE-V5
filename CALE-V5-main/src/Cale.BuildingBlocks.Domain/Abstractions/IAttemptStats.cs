namespace Cale.BuildingBlocks.Domain.Abstractions;

public sealed record AttemptSummary(
    int AttemptId,
    int UserId,
    int BankId,
    int? ExamId,
    string Mode,
    decimal Percent,
    bool Passed,
    DateTime StartedAt,
    DateTime? FinishedAt);

public interface IAttemptStats
{
    Task<IReadOnlyList<AttemptSummary>> ListByUserAsync(
        int userId,
        CancellationToken ct);

    Task<IReadOnlyList<AttemptSummary>> ListByUsersAsync(
        IReadOnlyList<int> userIds,
        CancellationToken ct);

    Task<int> CountAllAsync(CancellationToken ct);

    Task<int> CountFinishedByUserAndExamAsync(
        int userId,
        int examId,
        CancellationToken ct);

    Task<decimal?> BestPercentAsync(int userId, CancellationToken ct);
    Task<int> CountRatingsAsync(CancellationToken ct);
}
