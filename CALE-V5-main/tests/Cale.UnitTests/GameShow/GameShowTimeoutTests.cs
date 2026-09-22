using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Domain;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowTimeoutTests
{
    private static GameShowSession SessionWithRound(params (string text, int pts)[] answers)
    {
        var round = new GameShowRound
        {
            Id = 1,
            SortOrder = 0,
            QuestionText = "Q",
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
            TeamAName = "A",
            TeamBName = "B",
            CurrentRoundIndex = 0,
            TeamAScore = 0,
            TeamBScore = 0,
            Rounds = [round],
            Players =
            [
                new GameShowPlayer { Id = 1, DisplayName = "A1", Team = GameShowTeams.A, PlayerToken = Guid.NewGuid() },
                new GameShowPlayer { Id = 2, DisplayName = "B1", Team = GameShowTeams.B, PlayerToken = Guid.NewGuid() }
            ]
        };
    }

    [Fact]
    public void Timeout_before_deadline_is_noop()
    {
        var session = SessionWithRound(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        var now = DateTime.UtcNow;
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, now);

        var outcome = GameShowEngine.ProcessTimeout(session, round, now.AddSeconds(1));

        Assert.Equal(GameShowEngine.OutcomeKind.Noop, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.FaceOff, round.Phase);
        Assert.Equal(0, round.Strikes);
    }

    [Fact]
    public void FaceOff_timeout_passes_without_strike()
    {
        var session = SessionWithRound(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        var now = DateTime.UtcNow;
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, now);

        var outcome = GameShowEngine.ProcessTimeout(session, round, now.AddSeconds(26));

        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffMissPass, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.FaceOffSecond, round.Phase);
        Assert.Equal(GameShowTeams.B, round.ControllingTeam);
        Assert.Equal(0, round.Strikes);
        Assert.True(round.AnswerDeadlineUtc > now.AddSeconds(25));
    }

    [Fact]
    public void FaceOffSecond_timeout_reopens_WaitingBuzz()
    {
        var session = SessionWithRound(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        var now = DateTime.UtcNow;
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, now);
        GameShowEngine.ProcessTimeout(session, round, round.AnswerDeadlineUtc!.Value);

        var secondDeadline = round.AnswerDeadlineUtc!.Value;
        var outcome = GameShowEngine.ProcessTimeout(session, round, secondDeadline);

        Assert.Equal(GameShowEngine.OutcomeKind.FaceOffBothMissReopen, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.WaitingBuzz, round.Phase);
        Assert.Null(round.ControllingTeam);
        Assert.Null(round.AnswerDeadlineUtc);
    }

    [Fact]
    public void Control_timeout_adds_strike_then_opens_Steal()
    {
        var session = SessionWithRound(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        var now = DateTime.UtcNow;
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, now);
        GameShowEngine.ProcessAnswer(session, round, session.Players[0], "Frenos", now);
        Assert.Equal(GameShowRoundPhases.Control, round.Phase);

        var t1 = GameShowEngine.ProcessTimeout(session, round, round.AnswerDeadlineUtc!.Value);
        Assert.Equal(GameShowEngine.OutcomeKind.Strike, t1.Kind);
        Assert.Equal(1, round.Strikes);
        Assert.Equal(GameShowRoundPhases.Control, round.Phase);

        var t2 = GameShowEngine.ProcessTimeout(session, round, round.AnswerDeadlineUtc!.Value);
        Assert.Equal(2, round.Strikes);

        var t3 = GameShowEngine.ProcessTimeout(session, round, round.AnswerDeadlineUtc!.Value);
        Assert.Equal(GameShowEngine.OutcomeKind.StealOpportunity, t3.Kind);
        Assert.Equal(GameShowRoundPhases.Steal, round.Phase);
        Assert.Equal(3, round.Strikes);
    }

    [Fact]
    public void Steal_timeout_keeps_bank_with_controller()
    {
        var session = SessionWithRound(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        var now = DateTime.UtcNow;
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, now);
        GameShowEngine.ProcessAnswer(session, round, session.Players[0], "Frenos", now);
        round.RoundPointsForController = 75;
        round.Phase = GameShowRoundPhases.Steal;
        round.Strikes = 3;
        GameShowTiming.SetDeadline(round, now);

        var outcome = GameShowEngine.ProcessTimeout(session, round, now.AddSeconds(26));

        Assert.Equal(GameShowEngine.OutcomeKind.StealFailed, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.Finished, round.Phase);
        Assert.Equal(75, session.TeamAScore);
        Assert.Equal(0, session.TeamBScore);
        Assert.Null(round.AnswerDeadlineUtc);
    }

    [Fact]
    public void WaitingBuzz_has_no_deadline()
    {
        var session = SessionWithRound(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var round = session.Rounds[0];
        var now = DateTime.UtcNow;
        GameShowEngine.OpenBuzz(round, now);

        Assert.Null(round.AnswerDeadlineUtc);

        var outcome = GameShowEngine.ProcessTimeout(session, round, now.AddMinutes(5));

        Assert.Equal(GameShowEngine.OutcomeKind.Noop, outcome.Kind);
        Assert.Equal(GameShowRoundPhases.WaitingBuzz, round.Phase);
        Assert.Null(round.AnswerDeadlineUtc);
    }

    [Fact]
    public void Timing_constants_match_classroom_pacing()
    {
        Assert.Equal(0, GameShowTiming.BuzzWindow.TotalSeconds);
        Assert.Equal(25, GameShowTiming.FaceOffAnswer.TotalSeconds);
        Assert.Equal(30, GameShowTiming.ControlAnswer.TotalSeconds);
        Assert.Equal(25, GameShowTiming.StealAnswer.TotalSeconds);
    }
}
