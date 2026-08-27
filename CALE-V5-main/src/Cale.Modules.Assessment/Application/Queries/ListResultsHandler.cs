using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;

namespace Cale.Modules.Assessment.Application.Queries;

public sealed class ListResultsHandler
{
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
        var attempts = userId is not null
            ? await _attempts.ListByUserAsync(userId.Value, ct)
            : userIds is not null
                ? userIds.Count == 0
                    ? Array.Empty<Domain.Attempt>()
                    : await _attempts.ListByUsersAsync(userIds, ct)
                : await _attempts.ListFinishedAsync(ct);

        var finished = attempts
            .Where(x => x.FinishedAt is not null)
            .OrderByDescending(x => x.FinishedAt)
            .ToList();

        var nameCache = new Dictionary<int, string>();
        foreach (var id in finished.Select(x => x.UserId).Distinct())
        {
            nameCache[id] = await _users.GetNameAsync(id, ct) ?? "";
        }

        return finished.Select(attempt => new ResultRowDto(
            attempt.Id,
            attempt.UserId,
            nameCache.GetValueOrDefault(attempt.UserId, ""),
            attempt.Mode,
            attempt.Percent,
            attempt.Passed,
            attempt.StartedAt,
            attempt.FinishedAt)).ToList();
    }
}
