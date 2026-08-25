using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Assessment.Domain;

namespace Cale.Modules.Assessment.Application.Commands;

public sealed class SaveRatingHandler
{
    private readonly IAttemptStore _attempts;
    private readonly IClock _clock;

    public SaveRatingHandler(IAttemptStore attempts, IClock clock)
    {
        _attempts = attempts;
        _clock = clock;
    }

    public async Task HandleAsync(
        SaveRatingRequest request,
        int userId,
        CancellationToken ct)
    {
        var attempt = await _attempts.GetAsync(request.AttemptId, ct)
            ?? throw new NotFoundException("Attempt not found.", "attempt_not_found");
        attempt.EnsureOwned(userId);
        if (attempt.FinishedAt is null)
        {
            throw new ForbiddenException(
                "Rate only after finishing.",
                "attempt_not_finished");
        }

        if (await _attempts.FindRatingAsync(request.AttemptId, ct) is not null)
        {
            throw new ConflictException(
                "This attempt already has a rating.",
                "rating_exists");
        }

        var rating = AttemptRating.Create(
            userId,
            attempt.BankId,
            attempt.Id,
            request.Stars,
            request.Comment,
            _clock.UtcNow);
        await _attempts.AddRatingAsync(rating, ct);
        await _attempts.SaveChangesAsync(ct);
    }
}
