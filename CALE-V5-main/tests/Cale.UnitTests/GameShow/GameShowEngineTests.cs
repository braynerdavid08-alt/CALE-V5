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
        Assert.Equal(0, session.TeamAScore);
        Assert.Equal(0, session.TeamBScore);
    }

    [Fact]
    public void FaceOff_correct_grants_control_and_banks_points_only()
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
        Assert.Equal(0, session.TeamAScore); // still temporary
    }

    [Fact]
    public void Prompt_example_23_successful_steal_awards_bank_only()
    {
        // Equipo 1: Frenos 30, Luces 25, Llantas 20 = 75 banked, then 3 strikes, Equipo 2 steals with Espejos
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Frenos", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Luces", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Llantas", DateTime.UtcNow);
        Assert.Equal(75, round.RoundPointsForController);
        Assert.Equal(0, session.TeamAScore);

        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Motor", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Gasolina", DateTime.UtcNow);
        var strike3 = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.A), "Batería", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.StealOpportunity, strike3.Kind);
        Assert.Equal(GameShowRoundPhases.Steal, round.Phase);

        var steal = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.B), "Espejos", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.StealSucceeded, steal.Kind);
        Assert.Equal(GameShowRoundPhases.Finished, round.Phase);
        Assert.True(round.StealSucceeded);
        Assert.Equal(0, session.TeamAScore);
        Assert.Equal(75, session.TeamBScore); // bank only, not +15
    }

    [Fact]
    public void Prompt_example_24_failed_steal_keeps_bank_for_controller()
    {
        var session = SessionWithRound(
            ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A);

        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Frenos", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "Luces", DateTime.UtcNow);
        Assert.Equal(55, round.RoundPointsForController);

        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "x1", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "x2", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, Player(session, GameShowTeams.A), "x3", DateTime.UtcNow);
        Assert.Equal(GameShowRoundPhases.Steal, round.Phase);

        var fail = GameShowEngine.ProcessAnswer(
            session, round, Player(session, GameShowTeams.B), "Motor", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.StealFailed, fail.Kind);
        Assert.Equal(55, session.TeamAScore);
        Assert.Equal(0, session.TeamBScore);
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
        Assert.Equal(100, session.TeamAScore);
        Assert.Equal(0, session.TeamBScore);
    }

    [Fact]
    public void Already_revealed_does_not_consume_strike()
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
        Assert.Equal(GameShowRoundPhases.Control, round.Phase);
    }

    [Fact]
    public void Both_faceoff_misses_reopen_buzz()
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
    }

    [Fact]
    public void SuccessfulSteal_awards_bank_only()
    {
        Assert.Equal(75, GameShowScoringPolicy.ComputeSuccessfulStealPoints(75));
        Assert.Equal(0, GameShowScoringPolicy.ComputeSuccessfulStealPoints(0));
    }
}
