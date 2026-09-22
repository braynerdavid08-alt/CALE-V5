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
    public void SuccessfulSteal_awards_banked_points_only()
    {
        Assert.Equal(40, GameShowScoringPolicy.ComputeSuccessfulStealPoints(40));
    }

    [Theory]
    [InlineData("WaitingBuzz")]
    [InlineData("FaceOff")]
    [InlineData("FaceOffSecond")]
    [InlineData("Control")]
    [InlineData("Playing")]
    [InlineData("Steal")]
    [InlineData("Finished")]
    public void Round_phases_are_stable_constants(string phase)
    {
        Assert.Contains(phase, new[]
        {
            GameShowRoundPhases.WaitingBuzz,
            GameShowRoundPhases.FaceOff,
            GameShowRoundPhases.FaceOffSecond,
            GameShowRoundPhases.Control,
            GameShowRoundPhases.Playing,
            GameShowRoundPhases.Steal,
            GameShowRoundPhases.Finished
        });
    }

    [Fact]
    public void Playing_is_treated_as_control_for_legacy_sessions()
    {
        Assert.True(GameShowRoundPhases.IsControl(GameShowRoundPhases.Playing));
        Assert.True(GameShowRoundPhases.IsControl(GameShowRoundPhases.Control));
        Assert.True(GameShowRoundPhases.IsFaceOff(GameShowRoundPhases.FaceOff));
        Assert.True(GameShowRoundPhases.IsFaceOff(GameShowRoundPhases.FaceOffSecond));
    }
}
