using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Domain;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowScoringAndPhasesTests
{
    [Fact]
    public void MaxStrikes_is_three() =>
        Assert.Equal(3, GameShowScoringPolicy.MaxStrikes);

    [Fact]
    public void OppositeTeam_swaps_A_and_B()
    {
        Assert.Equal(GameShowTeams.B, GameShowScoringPolicy.OppositeTeam(GameShowTeams.A));
        Assert.Equal(GameShowTeams.A, GameShowScoringPolicy.OppositeTeam(GameShowTeams.B));
    }

    [Fact]
    public void SuccessfulSteal_awards_banked_plus_matched_points()
    {
        var pts = GameShowScoringPolicy.ComputeSuccessfulStealPoints(
            newlyRevealedPoints: 25,
            controllerBankedPoints: 40);
        Assert.Equal(65, pts);
    }

    [Theory]
    [InlineData("WaitingBuzz")]
    [InlineData("Playing")]
    [InlineData("Steal")]
    [InlineData("Finished")]
    public void Round_phases_are_stable_constants(string phase)
    {
        Assert.Contains(phase, new[]
        {
            GameShowRoundPhases.WaitingBuzz,
            GameShowRoundPhases.Playing,
            GameShowRoundPhases.Steal,
            GameShowRoundPhases.Finished
        });
    }
}
