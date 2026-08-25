using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;

namespace Cale.Modules.Assessment.Application.Queries;

public sealed class ListRatingsHandler
{
    private readonly IAttemptStore _attempts;
    private readonly IUserLookup _users;

    public ListRatingsHandler(IAttemptStore attempts, IUserLookup users)
    {
        _attempts = attempts;
        _users = users;
    }

    public async Task<IReadOnlyList<RatingDto>> HandleAsync(CancellationToken ct)
    {
        var ratings = await _attempts.ListRatingsAsync(ct);
        var result = new List<RatingDto>();
        foreach (var rating in ratings)
        {
            var name = await _users.GetNameAsync(rating.UserId, ct) ?? "";
            result.Add(new RatingDto(
                rating.Id,
                rating.UserId,
                name,
                rating.AttemptId,
                rating.Stars,
                rating.Comment,
                rating.Reviewed,
                rating.Hidden,
                rating.CreatedAt));
        }

        return result;
    }
}
