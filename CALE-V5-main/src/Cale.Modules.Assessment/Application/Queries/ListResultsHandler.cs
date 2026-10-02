using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;

namespace Cale.Modules.Assessment.Application.Queries;

public sealed class ListResultsHandler
{
    // Results pages group and chart client-side; the newest rows are what staff review,
    // and an unbounded list grows with every simulacro taken.
    public const int MaxRows = 5000;

    private readonly IAttemptStore _attempts;
    private readonly IUserLookup _users;

    public ListResultsHandler(IAttemptStore attempts, IUserLookup users)
    {
        _attempts = attempts;
        _users = users;
    }

    public async Task<IReadOnlyList<ResultRowDto>> HandleAsync(
        int? userId,
        IReadOnlyList<int>? userIds,
        CancellationToken ct)
    {
        var finished = await _attempts.ListResultRowsAsync(userId, userIds, MaxRows, ct);
        if (finished.Count == 0)
        {
            return [];
        }

        var names = await _users.GetNamesAsync(
            finished.Select(x => x.UserId).Distinct().ToList(),
            ct);

        return finished.Select(attempt => new ResultRowDto(
            attempt.Id,
            attempt.UserId,
            names.GetValueOrDefault(attempt.UserId, ""),
            attempt.Mode,
            attempt.Percent,
            attempt.Passed,
            attempt.StartedAt,
            attempt.FinishedAt,
            attempt.TimeSeconds > 0 ? attempt.TimeSeconds : null)).ToList();
    }
}
