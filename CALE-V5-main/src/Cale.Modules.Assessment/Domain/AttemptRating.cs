using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Assessment.Domain;

public sealed class AttemptRating
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public int? BankId { get; private set; }
    public int? AttemptId { get; private set; }
    public int Stars { get; private set; }
    public string? Comment { get; private set; }
    public string? Critique { get; private set; }
    public bool Reviewed { get; private set; }
    public bool Hidden { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AttemptRating()
    {
    }

    public static AttemptRating Create(
        int userId,
        int? bankId,
        int attemptId,
        int stars,
        string? comment,
        DateTime utcNow)
    {
        if (stars is < 1 or > 5)
        {
            throw new DomainException(
                "Rating must be between 1 and 5.",
                400,
                "invalid_stars");
        }

        return new AttemptRating
        {
            UserId = userId,
            BankId = bankId,
            AttemptId = attemptId,
            Stars = stars,
            Comment = comment?.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }
}
