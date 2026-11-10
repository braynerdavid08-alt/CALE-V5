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

    [Theory]
    [InlineData(3, 1, 1)]
    [InlineData(4, 2, 0)]
    [InlineData(5, 2, 1)]
    [InlineData(7, 3, 1)]
    [InlineData(9, 4, 1)]
    [InlineData(11, 5, 1)]
    [InlineData(15, 7, 1)]
    [InlineData(16, 8, 0)]
    [InlineData(8, 4, 0)]
    [InlineData(13, 6, 1)]
    public void PairRound_Maximizes_Real_Matches_With_At_Most_One_Bye(
        int players,
        int expectedReal,
        int expectedByes)
    {
        var ids = Enumerable.Range(1, players).ToList();
        var pairs = TournamentBracket.PairRound(ids, new Random(7));
        Assert.Equal(expectedReal + expectedByes, pairs.Count);
        Assert.Equal(expectedByes, pairs.Count(p => p.P1Bye ^ p.P2Bye));
        Assert.Equal(expectedReal, pairs.Count(p => !p.P1Bye && !p.P2Bye));
        Assert.DoesNotContain(pairs, p => p.P1Bye && p.P2Bye);

        var seated = pairs
            .SelectMany(p => new[] { p.Player1, p.Player2 })
            .Where(id => id is int)
            .Select(id => id!.Value)
            .ToList();
        Assert.Equal(players, seated.Count);
        Assert.Equal(players, seated.Distinct().Count());
        Assert.Equal(ids.OrderBy(x => x), seated.OrderBy(x => x));
    }

    [Fact]
    public void PairRound_Dedupes_Corrupt_Input()
    {
        var pairs = TournamentBracket.PairRound([1, 1, 2, 3, 3], new Random(1));
        var seated = pairs
            .SelectMany(p => new[] { p.Player1, p.Player2 })
            .Where(id => id is int)
            .Select(id => id!.Value)
            .ToList();
        Assert.Equal(3, seated.Distinct().Count());
        Assert.Equal(seated.Count, seated.Distinct().Count());
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(7, 3)]
    [InlineData(9, 4)]
    [InlineData(11, 4)]
    [InlineData(15, 4)]
    [InlineData(16, 4)]
    public void EstimatedRoundCount_Matches_Living_Field(int players, int expectedRounds) =>
        Assert.Equal(expectedRounds, TournamentBracket.EstimatedRoundCount(players));

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
    public void RoundNameForField_Uses_Living_Field_Size()
    {
        Assert.Equal("Gran final", TournamentBracket.RoundNameForField(2));
        Assert.Equal("Semifinal", TournamentBracket.RoundNameForField(3));
        Assert.Equal("Semifinal", TournamentBracket.RoundNameForField(4));
        Assert.Equal("Octavos", TournamentBracket.RoundNameForField(9));
    }

    [Fact]
    public void RoundName_FinalAndSemis()
    {
        Assert.Equal("Gran final", TournamentBracket.RoundName(4, 4));
        Assert.Equal("Semifinal", TournamentBracket.RoundName(3, 4));
        Assert.Equal("Cuartos", TournamentBracket.RoundName(2, 4));
    }
}
