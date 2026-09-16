using Cale.BuildingBlocks.Domain.Scoring;

namespace Cale.UnitTests;

public class ScoringRulesTests
{
    [Theory]
    [InlineData(7, 10, true)]
    [InlineData(6, 10, false)]
    [InlineData(37, 40, true)]
    [InlineData(36, 40, false)]
    [InlineData(22, 25, true)]
    [InlineData(21, 25, false)]
    public void Pass_With_At_Most_Three_Incorrect(
        int correct,
        int total,
        bool expected)
    {
        Assert.Equal(expected, ScoringRules.IsPassed(correct, total));
    }
}
