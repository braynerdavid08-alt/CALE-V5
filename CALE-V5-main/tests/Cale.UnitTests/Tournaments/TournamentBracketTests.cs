using Cale.Api.Services.Tournaments;

namespace Cale.UnitTests.Tournaments;

public sealed class TournamentBracketTests
{
    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    [InlineData(13, 16)]
    [InlineData(16, 16)]
    public void NextPowerOfTwo_RoundsUp(int count, int expected) =>
        Assert.Equal(expected, TournamentBracket.NextPowerOfTwo(count));

    [Fact]
    public void SeedSlots_PlacesByesForThirteenPlayers()
    {
        var ids = Enumerable.Range(1, 13).ToList();
        var slots = TournamentBracket.SeedSlots(ids, 16, new Random(42));
        Assert.Equal(16, slots.Length);
        Assert.Equal(13, slots.Count(s => s is not null));
        Assert.Equal(3, slots.Count(s => s is null));
        Assert.Equal(ids.OrderBy(x => x), slots.Where(s => s is not null).Cast<int>().OrderBy(x => x));

        var pairs = TournamentBracket.FirstRoundPairs(slots);
        Assert.Equal(3, pairs.Count(p => p.P1Bye ^ p.P2Bye));
        Assert.DoesNotContain(pairs, p => p.P1Bye && p.P2Bye);
    }

    [Fact]
    public void FirstRoundPairs_MarksByeSides()
    {
        int?[] slots = [1, null, 2, null, 3, 4, 5, 6];
        var pairs = TournamentBracket.FirstRoundPairs(slots);
        Assert.Equal(4, pairs.Count);
        Assert.True(pairs[0].P2Bye);
        Assert.True(pairs[1].P2Bye);
        Assert.False(pairs[2].P1Bye || pairs[2].P2Bye);
    }

    [Fact]
    public void RoundName_FinalAndSemis()
    {
        Assert.Equal("Gran final", TournamentBracket.RoundName(4, 4));
        Assert.Equal("Semifinal", TournamentBracket.RoundName(3, 4));
        Assert.Equal("Cuartos", TournamentBracket.RoundName(2, 4));
    }
}
