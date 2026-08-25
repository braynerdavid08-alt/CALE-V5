using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;

namespace Cale.Modules.Assessment.Application.Commands;

public sealed class ManageRatingHandler
{
    private readonly IAttemptStore _attempts;
    private readonly IClock _clock;

    public ManageRatingHandler(IAttemptStore attempts, IClock clock)
    {
        _attempts = attempts;
        _clock = clock;
    }

    public async Task HandleAsync(
        int ratingId,
        ManageRatingRequest request,
        CancellationToken ct)
    {
        var rating = await _attempts.GetRatingByIdAsync(ratingId, ct)
            ?? throw new NotFoundException("Rating not found.", "rating_not_found");

        if (request.Reviewed == true)
        {
            rating.MarkReviewed(request.Critique, _clock.UtcNow);
        }

        if (request.Hidden is not null)
        {
            rating.SetHidden(request.Hidden.Value, _clock.UtcNow);
        }

        await _attempts.SaveChangesAsync(ct);
    }
}
