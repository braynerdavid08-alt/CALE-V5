using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Assessment.Application.Abstractions;

namespace Cale.Modules.Assessment.Infrastructure;

public sealed class AttemptStatsService : IAttemptStats
{
    private readonly IAttemptStore _store;

    public AttemptStatsService(IAttemptStore store) => _store = store;

    public async Task<IReadOnlyList<AttemptSummary>> ListByUserAsync(
        int userId,
        CancellationToken ct)
    {
        var items = await _store.ListByUserAsync(userId, ct);
        return items.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<AttemptSummary>> ListByUsersAsync(
        IReadOnlyList<int> userIds,
        CancellationToken ct)
    {
        var items = await _store.ListByUsersAsync(userIds, ct);
        return items.Select(Map).ToList();
    }

    public Task<int> CountAllAsync(CancellationToken ct) =>
        _store.CountAllAsync(ct);

    public Task<int> CountFinishedByUserAndExamAsync(
        int userId,
        int examId,
        CancellationToken ct) =>
        _store.CountFinishedByUserAndExamAsync(userId, examId, ct);

    public async Task<decimal?> BestPercentAsync(int userId, CancellationToken ct)
    {
        var items = await _store.ListByUserAsync(userId, ct);
        var finished = items.Where(x => x.FinishedAt is not null).ToList();
        return finished.Count == 0 ? null : finished.Max(x => x.Percent);
    }

    public async Task<int> CountRatingsAsync(CancellationToken ct)
    {
        var ratings = await _store.ListRatingsAsync(ct);
        return ratings.Count;
    }

    private static AttemptSummary Map(Domain.Attempt x) => new(
        x.Id,
        x.UserId,
        x.BankId,
        x.ExamId,
        x.Mode,
        x.Percent,
        x.Passed,
        x.StartedAt,
        x.FinishedAt);
}
