using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Domain;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowMultiTeamTests
{
    private static GameShowSession Session(int teamCount, params string[] playerTeams)
    {
        var t0 = DateTime.UtcNow;
        var round = new GameShowRound
        {
            Id = 1,
            QuestionText = "¿Qué se revisa antes de arrancar?",
            Phase = GameShowRoundPhases.WaitingBuzz,
            Answers =
            [
                new GameShowBoardAnswer { Id = 1, Rank = 1, Text = "Frenos", Points = 40, AliasesJson = "[]" },
                new GameShowBoardAnswer { Id = 2, Rank = 2, Text = "Luces", Points = 30, AliasesJson = "[]" },
                new GameShowBoardAnswer { Id = 3, Rank = 3, Text = "Llantas", Points = 20, AliasesJson = "[]" }
            ]
        };
        return new GameShowSession
        {
            Id = 1,
            Status = GameShowSessionStatuses.Running,
            TeamCount = teamCount,
            CurrentRoundIndex = 0,
            Rounds = [round],
            Players = playerTeams
                .Select((team, i) => new GameShowPlayer
                {
                    Id = i + 1,
                    DisplayName = $"P{i + 1}",
                    Team = team,
                    JoinedAt = t0.AddSeconds(i),
                    PlayerToken = Guid.NewGuid()
                })
                .ToList()
        };
    }

    private static GameShowPlayer Of(GameShowSession s, string team) => s.Players.First(p => p.Team == team);

    [Fact]
    public void Face_off_miss_passes_through_every_team_before_reopening()
    {
        var s = Session(4, "A", "B", "C", "D");
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, "C", DateTime.UtcNow, s);

        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffMissPass, GameShowEngine.ProcessAnswer(s, r, Of(s, "C"), "nada", DateTime.UtcNow).Kind);
        Assert.Equal("D", r.ControllingTeam);
        GameShowEngine.ProcessAnswer(s, r, Of(s, "D"), "nada", DateTime.UtcNow);
        Assert.Equal("A", r.ControllingTeam);
        GameShowEngine.ProcessAnswer(s, r, Of(s, "A"), "nada", DateTime.UtcNow);
        Assert.Equal("B", r.ControllingTeam);

        var last = GameShowEngine.ProcessAnswer(s, r, Of(s, "B"), "nada", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffBothMissReopen, last.Kind);
        Assert.Equal(GameShowRoundPhases.WaitingBuzz, r.Phase);
    }

    [Fact]
    public void Face_off_skips_empty_teams()
    {
        var s = Session(4, "A", "C");
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, "A", DateTime.UtcNow, s);

        GameShowEngine.ProcessAnswer(s, r, Of(s, "A"), "nada", DateTime.UtcNow);
        Assert.Equal("C", r.ControllingTeam);
        var last = GameShowEngine.ProcessAnswer(s, r, Of(s, "C"), "nada", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffBothMissReopen, last.Kind);
    }

    [Fact]
    public void Three_strikes_with_several_rivals_opens_steal_buzzer_and_stealer_scores()
    {
        var s = Session(3, "A", "B", "C");
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, "A", DateTime.UtcNow, s);
        GameShowEngine.ProcessAnswer(s, r, Of(s, "A"), "Frenos", DateTime.UtcNow);
        for (var i = 0; i < 3; i++)
        {
            GameShowEngine.ProcessAnswer(s, r, Of(s, "A"), "nada", DateTime.UtcNow);
        }

        Assert.Equal(GameShowRoundPhases.StealBuzz, r.Phase);
        Assert.Null(r.ActivePlayerId);
        Assert.Null(GameShowEngine.AnsweringTeam(r));

        GameShowEngine.EnterSteal(s, r, "C", DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() =>
            GameShowEngine.ProcessAnswer(s, r, Of(s, "B"), "Luces", DateTime.UtcNow));

        var outcome = GameShowEngine.ProcessAnswer(s, r, Of(s, "C"), "Luces", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.StealSucceeded, outcome.Kind);
        Assert.Equal(40, s.TeamCScore);
        Assert.Equal(0, s.TeamAScore);
    }

    [Fact]
    public void Nobody_claiming_the_steal_leaves_points_with_controller()
    {
        var s = Session(4, "A", "B", "C", "D");
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, "B", DateTime.UtcNow, s);
        GameShowEngine.ProcessAnswer(s, r, Of(s, "B"), "Frenos", DateTime.UtcNow);
        for (var i = 0; i < 3; i++)
        {
            GameShowEngine.ProcessAnswer(s, r, Of(s, "B"), "nada", DateTime.UtcNow);
        }

        var outcome = GameShowEngine.ProcessTimeout(s, r, r.AnswerDeadlineUtc!.Value.AddSeconds(1));
        Assert.Equal(GameShowEngine.OutcomeKind.StealFailed, outcome.Kind);
        Assert.Equal(40, s.TeamBScore);
    }

    [Fact]
    public void Two_teams_still_go_straight_to_the_steal()
    {
        var s = Session(2, "A", "B");
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, "A", DateTime.UtcNow, s);
        GameShowEngine.ProcessAnswer(s, r, Of(s, "A"), "Frenos", DateTime.UtcNow);
        for (var i = 0; i < 3; i++)
        {
            GameShowEngine.ProcessAnswer(s, r, Of(s, "A"), "nada", DateTime.UtcNow);
        }

        Assert.Equal(GameShowRoundPhases.Steal, r.Phase);
        Assert.Equal("B", r.StealingTeam);
    }

    [Fact]
    public void Leaders_detects_single_winner_and_ties()
    {
        var s = Session(3);
        s.TeamCScore = 50;
        Assert.Equal(["C"], GameShowTeams.Leaders(s));
        s.TeamAScore = 50;
        Assert.Equal(2, GameShowTeams.Leaders(s).Count);
        Assert.Null(GameShowTeams.Normalize(s, "D"));
        Assert.Equal("C", GameShowTeams.Normalize(s, "c"));
    }
}
