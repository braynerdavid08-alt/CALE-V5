using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Domain;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowEngineTests
{
    private static GameShowSession SessionWithRound(params (string text, int pts)[] answers)
    {
        var round = new GameShowRound
        {
            Id = 1,
            SortOrder = 0,
            QuestionText = "¿Qué debe revisar un conductor antes de iniciar un viaje?",
            Phase = GameShowRoundPhases.WaitingBuzz,
            Answers = answers
                .Select((a, i) => new GameShowBoardAnswer
                {
                    Id = i + 1,
                    Rank = i + 1,
                    Text = a.text,
                    Points = a.pts,
                    AliasesJson = "[]"
                })
                .ToList()
        };
        return new GameShowSession
        {
            Id = 10,
            Title = "Test",
            Status = GameShowSessionStatuses.Running,
            TeamAName = "Equipo 1",
            TeamBName = "Equipo 2",
            CurrentRoundIndex = 0,
            TeamAScore = 150, // preexisting score must not change mid-control
            TeamBScore = 40,
            Rounds = [round],
            Players =
            [
                new GameShowPlayer { Id = 1, DisplayName = "A1", Team = GameShowTeams.A, PlayerToken = Guid.NewGuid() },
                new GameShowPlayer { Id = 2, DisplayName = "B1", Team = GameShowTeams.B, PlayerToken = Guid.NewGuid() }
            ]
        };
    }

    private static GameShowPlayer Player(GameShowSession s, string team) =>
        s.Players.First(p => p.Team == team);

    [Fact]
    public void FaceOff_first_correct_grants_control_and_banks_only()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        var outcome = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.A), "Frenos", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffWonControl, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.Control, round.Phase);
        Assert.Equal(GameShowTeams.A, round.ControllingTeam);
        Assert.Equal(30, round.RoundPointsForController);
        Assert.Equal(150, session.TeamAScore);
        Assert.Equal(40, session.TeamBScore);
    }

    [Fact]
    public void FaceOff_miss_passes_to_other_team_without_strike()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        var outcome = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.A), "Motor", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffMissPass, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.FaceOffSecond, round.Phase);
        Assert.Equal(GameShowTeams.B, round.ControllingTeam);
        Assert.Equal(0, round.Strikes);
        Assert.Equal(150, session.TeamAScore);
        Assert.Equal(40, session.TeamBScore);
    }

    [Fact]
    public void FaceOffSecond_correct_grants_control_to_second_team()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "xxx", DateTime.UtcNow);
        var win = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.B), "Frenos", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffWonControl, win.Kind);
        Assert.Equal(GameShowRoundPhases.Control, round.Phase);
        Assert.Equal(GameShowTeams.B, round.ControllingTeam);
        Assert.Equal(0, round.Strikes);
        Assert.Equal(30, round.RoundPointsForController);
        Assert.Equal(150, session.TeamAScore);
        Assert.Equal(40, session.TeamBScore);
    }

    [Fact]
    public void Both_faceoff_misses_reopen_WaitingBuzz()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "xxx", DateTime.UtcNow);
        var second = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.B), "yyy", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffBothMissReopen, second.Kind);
        Assert.Equal(GameShowRoundPhases.WaitingBuzz, round.Phase);
        Assert.Null(round.ControllingTeam);
        Assert.Equal(0, round.Strikes);
    }

    [Fact]
    public void Control_strikes_progress_then_open_Steal_without_finishing()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Frenos", DateTime.UtcNow);

        var s1 = GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "x1", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.Strike, s1.Kind);
        Assert.Equal(1, round.Strikes);
        Assert.Equal(GameShowRoundPhases.Control, round.Phase);

        var s2 = GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "x2", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.Strike, s2.Kind);
        Assert.Equal(2, round.Strikes);
        Assert.Equal(GameShowRoundPhases.Control, round.Phase);

        var s3 = GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "x3", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.StealOpportunity, s3.Kind);
        Assert.Equal(3, round.Strikes);
        Assert.Equal(GameShowRoundPhases.Steal, round.Phase);
        Assert.Equal(150, session.TeamAScore); // still not committed
    }

    [Fact]
    public void Control_correct_banks_without_touching_scoreboard()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Frenos", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Luces", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Llantas", DateTime.UtcNow);

        Assert.Equal(75, round.RoundPointsForController);
        Assert.Equal(150, session.TeamAScore);
        Assert.Equal(40, session.TeamBScore);
    }

    [Fact]
    public void Prompt_example_successful_steal_awards_bank_only_not_stolen_answer_points()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Frenos", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Luces", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Llantas", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Motor", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Gasolina", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Batería", DateTime.UtcNow);
        Assert.Equal(GameShowRoundPhases.Steal, round.Phase);
        Assert.Equal(75, round.RoundPointsForController);

        var steal = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.B), "Espejos", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.StealSucceeded, steal.Kind);
        Assert.Equal(GameShowRoundPhases.Finished, round.Phase);
        Assert.True(round.StealSucceeded);
        Assert.Equal(150, session.TeamAScore); // unchanged
        Assert.Equal(40 + 75, session.TeamBScore); // bank only, not +15
    }

    [Fact]
    public void Failed_steal_commits_bank_to_controller()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Frenos", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Luces", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "x1", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "x2", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "x3", DateTime.UtcNow);

        var fail = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.B), "Motor", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.StealFailed, fail.Kind);
        Assert.Equal(150 + 55, session.TeamAScore);
        Assert.Equal(40, session.TeamBScore);
    }

    [Fact]
    public void Completing_all_five_commits_100_without_steal()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        foreach (var ans in new[] { "Frenos", "Luces", "Llantas", "Espejos", "Aceite" })
        {
            GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), ans, DateTime.UtcNow);
        }

        Assert.Equal(GameShowRoundPhases.Finished, round.Phase);
        Assert.NotEqual(GameShowRoundPhases.Steal, round.Phase);
        Assert.Equal(150 + 100, session.TeamAScore);
        Assert.Equal(40, session.TeamBScore);
    }

    [Fact]
    public void Already_revealed_does_not_consume_strike_or_points()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Frenos", DateTime.UtcNow);

        var dup = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.A), "Frenos", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.IgnoredAlreadyRevealed, dup.Kind);
        Assert.Equal(0, round.Strikes);
        Assert.Equal(30, round.RoundPointsForController);
        Assert.Equal(150, session.TeamAScore);
        Assert.Equal(GameShowRoundPhases.Control, round.Phase);
    }

    [Fact]
    public void Host_end_during_faceoff_reopens_buzz_instead_of_finishing()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        var outcome = GameShowEngine.HostEndRound(session, round, DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffBothMissReopen, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.WaitingBuzz, round.Phase);
        Assert.Null(round.ControllingTeam);
    }

    [Fact]
    public void SuccessfulSteal_policy_is_bank_only()
    {
        Assert.Equal(75, GameShowScoringPolicy.ComputeSuccessfulStealPoints(75));
        Assert.Equal(0, GameShowScoringPolicy.ComputeSuccessfulStealPoints(0));
    }
}
