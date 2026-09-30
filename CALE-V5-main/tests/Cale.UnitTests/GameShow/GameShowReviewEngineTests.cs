using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Application.DTOs;
using Cale.Modules.GameShow.Domain;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowReviewEngineTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static GameShowSession SessionWithRound()
    {
        var answers = new[] { ("Frenos", 30), ("Luces", 25), ("Llantas", 20) };
        var round = new GameShowRound
        {
            Id = 1,
            QuestionText = "¿Qué debe revisar un conductor antes de iniciar un viaje?",
            Phase = GameShowRoundPhases.WaitingBuzz,
            Answers = answers
                .Select((a, i) => new GameShowBoardAnswer { Id = i + 1, Rank = i + 1, Text = a.Item1, Points = a.Item2, AliasesJson = "[]" })
                .ToList()
        };
        return new GameShowSession
        {
            Id = 10,
            Title = "Test",
            Status = GameShowSessionStatuses.Running,
            TeamAName = "A",
            TeamBName = "B",
            TeamAScore = 100,
            TeamBScore = 50,
            Rounds = [round],
            Players =
            [
                new GameShowPlayer { Id = 1, DisplayName = "A1", Team = GameShowTeams.A, PlayerToken = Guid.NewGuid() },
                new GameShowPlayer { Id = 2, DisplayName = "B1", Team = GameShowTeams.B, PlayerToken = Guid.NewGuid() }
            ]
        };
    }

    private static GameShowPlayer P(GameShowSession s, string team) => s.Players.First(p => p.Team == team);

    private static GameShowAttempt Last(GameShowRound r) => GameShowReviewEngine.LatestAttempt(r)!;

    [Fact]
    public void Accepting_control_strike_removes_strike_and_banks_answer()
    {
        var s = SessionWithRound();
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, GameShowTeams.A);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "Frenos", T0);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "pito", T0.AddSeconds(5));
        Assert.Equal(1, r.Strikes);

        var attempt = Last(r);
        Assert.Equal(GameShowReviewEngine.AcceptMode.ControlStrike, GameShowReviewEngine.GetAcceptMode(r, attempt, true));
        GameShowReviewEngine.Accept(s, r, attempt, r.Answers[1], T0.AddSeconds(6));

        Assert.Equal(0, r.Strikes);
        Assert.True(r.Answers[1].IsRevealed);
        Assert.Equal(55, r.RoundPointsForController);
        Assert.True(attempt.IsCorrect);
    }

    [Fact]
    public void Accepting_strike_that_opened_steal_returns_to_control()
    {
        var s = SessionWithRound();
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, GameShowTeams.A);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "Frenos", T0);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "x1", T0.AddSeconds(1));
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "x2", T0.AddSeconds(2));
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "x3", T0.AddSeconds(3));
        Assert.Equal(GameShowRoundPhases.Steal, r.Phase);

        GameShowReviewEngine.Accept(s, r, Last(r), r.Answers[2], T0.AddSeconds(4));

        Assert.Equal(GameShowRoundPhases.Control, r.Phase);
        Assert.Equal(2, r.Strikes);
        Assert.Equal(50, r.RoundPointsForController);
    }

    [Fact]
    public void Accepting_failed_steal_moves_bank_to_stealer()
    {
        var s = SessionWithRound();
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, GameShowTeams.A);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "Frenos", T0);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "x1", T0.AddSeconds(1));
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "x2", T0.AddSeconds(2));
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "x3", T0.AddSeconds(3));
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.B), "casco", T0.AddSeconds(4));
        Assert.Equal(GameShowRoundPhases.Finished, r.Phase);
        Assert.Equal(130, s.TeamAScore);

        var attempt = Last(r);
        Assert.Equal(GameShowReviewEngine.AcceptMode.Steal, GameShowReviewEngine.GetAcceptMode(r, attempt, true));
        GameShowReviewEngine.Accept(s, r, attempt, r.Answers[1], T0.AddSeconds(5));

        Assert.Equal(100, s.TeamAScore);
        Assert.Equal(80, s.TeamBScore);
        Assert.True(r.StealSucceeded);
    }

    [Fact]
    public void Accepting_faceoff_miss_gives_control_to_that_team()
    {
        var s = SessionWithRound();
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, GameShowTeams.A);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "revisar frenado", T0);
        Assert.Equal(GameShowRoundPhases.FaceOffSecond, r.Phase);

        GameShowReviewEngine.Accept(s, r, Last(r), r.Answers[0], T0.AddSeconds(1));

        Assert.Equal(GameShowRoundPhases.Control, r.Phase);
        Assert.Equal(GameShowTeams.A, r.ControllingTeam);
        Assert.Equal(30, r.RoundPointsForController);
        Assert.Equal(0, r.Strikes);
    }

    [Fact]
    public void Repeat_of_revealed_answer_cannot_be_accepted()
    {
        var s = SessionWithRound();
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, GameShowTeams.A);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "Frenos", T0);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "x1", T0.AddSeconds(1));
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "frenos", T0.AddSeconds(2));

        Assert.Equal(GameShowReviewEngine.AcceptMode.None, GameShowReviewEngine.GetAcceptMode(r, Last(r), true));
    }

    [Fact]
    public void Rejecting_false_positive_hides_answer_and_adds_strike()
    {
        var s = SessionWithRound();
        var r = s.Rounds[0];
        GameShowEngine.EnterFaceOff(r, GameShowTeams.A);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "Frenos", T0);
        GameShowEngine.ProcessAnswer(s, r, P(s, GameShowTeams.A), "Luces", T0.AddSeconds(1));
        var attempt = Last(r);
        Assert.True(GameShowReviewEngine.CanReject(r, attempt, true));

        GameShowReviewEngine.Reject(s, r, attempt, T0.AddSeconds(2));

        Assert.False(r.Answers[1].IsRevealed);
        Assert.Equal(30, r.RoundPointsForController);
        Assert.Equal(1, r.Strikes);
        Assert.False(attempt.IsCorrect);
    }

    [Fact]
    public void Official_aliases_merge_into_pack_answers()
    {
        var body = new CreateGameShowRequest("P", "A", "B",
        [
            new CreateGameShowRoundRequest("¿Qué revisas antes de salir?", null,
                [new CreateGameShowAnswerRequest("Frenos", 30, ["freno"])])
        ]);
        var map = GameShowOfficialAliases.Parse(null);
        GameShowOfficialAliases.Add(map, "¿que revisas antes de salir", "frenos", "el frenado");

        var merged = GameShowOfficialAliases.Merge(body, GameShowOfficialAliases.Serialize(map));

        Assert.Equal(["freno", "el frenado"], merged.Rounds[0].Answers[0].Aliases);
    }
}
