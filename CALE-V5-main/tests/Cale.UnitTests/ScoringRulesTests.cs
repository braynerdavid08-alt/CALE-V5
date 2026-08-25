using Cale.BuildingBlocks.Domain.Scoring;

namespace Cale.UnitTests;

public class ScoringRulesTests
{
    [Theory]
    [InlineData(79.99, false)]
    [InlineData(80, true)]
    [InlineData(100, true)]
    public void Pass_At_Eighty_Percent(decimal percent, bool expected)
    {
        Assert.Equal(expected, ScoringRules.IsPassed(percent));
    }
}
