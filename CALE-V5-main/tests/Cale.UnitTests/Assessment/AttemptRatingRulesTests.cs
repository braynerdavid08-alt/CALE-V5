using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Assessment.Domain;

namespace Cale.UnitTests.Assessment;

public sealed class AttemptRatingRulesTests
{
    [Fact]
    public void Create_requires_comment()
    {
        var ex = Assert.Throws<DomainException>(() =>
            AttemptRating.Create(1, 1, 10, 5, "  ", DateTime.UtcNow));

        Assert.Equal("comment_required", ex.ErrorCode);
    }

    [Fact]
    public void Create_accepts_comment_and_stars()
    {
        var rating = AttemptRating.Create(1, 2, 10, 4, "  Muy útil para practicar  ", DateTime.UtcNow);

        Assert.Equal(4, rating.Stars);
        Assert.Equal("Muy útil para practicar", rating.Comment);
        Assert.Equal(1, rating.UserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_rejects_invalid_stars(int stars)
    {
        var ex = Assert.Throws<DomainException>(() =>
            AttemptRating.Create(1, 1, 10, stars, "Comentario válido", DateTime.UtcNow));

        Assert.Equal("invalid_stars", ex.ErrorCode);
    }
}
