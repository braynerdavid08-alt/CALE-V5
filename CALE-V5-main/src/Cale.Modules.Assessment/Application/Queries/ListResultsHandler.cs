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

        var rows = new List<ResultRowDto>();
        foreach (var attempt in attempts.Where(x => x.FinishedAt is not null)
                     .OrderByDescending(x => x.FinishedAt))
        {
            var name = await _users.GetNameAsync(attempt.UserId, ct) ?? "";
            rows.Add(new ResultRowDto(
                attempt.Id,
                attempt.UserId,
                name,
                attempt.Mode,
                attempt.Percent,
                attempt.Passed,
                attempt.StartedAt,
                attempt.FinishedAt));
        }

        return rows;
    }
}
