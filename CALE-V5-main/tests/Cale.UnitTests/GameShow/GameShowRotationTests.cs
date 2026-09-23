using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Domain;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowRotationTests
{
    private static GameShowSession SessionWithRoster(
        (string text, int pts)[] answers,
        params (int id, string name, string team, DateTime joined)[] players)
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
            TeamAScore = 0,
            TeamBScore = 0,
            Rounds = [round],
            Players = players
                .Select(p => new GameShowPlayer
                {
                    Id = p.id,
                    DisplayName = p.name,
                    Team = p.team,
                    JoinedAt = p.joined,
                    PlayerToken = Guid.NewGuid()
                })
                .ToList()
        };
    }

    private static (string text, int pts)[] DefaultAnswers() =>
    [
        ("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10)
    ];

    private static GameShowPlayer P(GameShowSession s, int id) =>
        s.Players.First(x => x.Id == id);

    [Fact]
    public void EnterFaceOff_preferred_player_becomes_ActivePlayer()
    {
        var t0 = DateTime.UtcNow;
        var session = SessionWithRoster(
            DefaultAnswers(),
            (1, "A1", GameShowTeams.A, t0),
            (2, "A2", GameShowTeams.A, t0.AddSeconds(1)),
            (3, "B1", GameShowTeams.B, t0));
        var round = session.Rounds[0];

        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, DateTime.UtcNow, session, preferredPlayerId: 2);

        Assert.Equal(2, round.ActivePlayerId);
    }

    [Fact]
    public void Control_rotates_to_next_teammate_after_correct_and_wrong()
    {
        var t0 = DateTime.UtcNow;
        var session = SessionWithRoster(
            DefaultAnswers(),
            (1, "A1", GameShowTeams.A, t0),
            (2, "A2", GameShowTeams.A, t0.AddSeconds(1)),
            (3, "A3", GameShowTeams.A, t0.AddSeconds(2)),
            (4, "B1", GameShowTeams.B, t0));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, DateTime.UtcNow, session, preferredPlayerId: 1);

        GameShowEngine.ProcessAnswer(session, round, P(session, 1), "Frenos", DateTime.UtcNow);
        Assert.Equal(GameShowRoundPhases.Control, round.Phase);
        Assert.Equal(2, round.ActivePlayerId);

        GameShowEngine.ProcessAnswer(session, round, P(session, 2), "Luces", DateTime.UtcNow);
        Assert.Equal(3, round.ActivePlayerId);

        var strike = GameShowEngine.ProcessAnswer(session, round, P(session, 3), "zzz", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.Strike, strike.Kind);
        Assert.Equal(1, round.ActivePlayerId);
    }

    [Fact]
    public void ProcessAnswer_teammate_who_is_not_ActivePlayer_throws_not_your_turn()
    {
        var t0 = DateTime.UtcNow;
        var session = SessionWithRoster(
            DefaultAnswers(),
            (1, "A1", GameShowTeams.A, t0),
            (2, "A2", GameShowTeams.A, t0.AddSeconds(1)),
            (3, "B1", GameShowTeams.B, t0));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, DateTime.UtcNow, session, preferredPlayerId: 1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GameShowEngine.ProcessAnswer(session, round, P(session, 2), "Frenos", DateTime.UtcNow));
        Assert.Equal("not_your_turn", ex.Message);
        Assert.Equal(1, round.ActivePlayerId);
    }

    [Fact]
    public void Steal_assigns_ActivePlayer_to_first_rival()
    {
        var t0 = DateTime.UtcNow;
        var session = SessionWithRoster(
            DefaultAnswers(),
            (1, "A1", GameShowTeams.A, t0),
            (2, "B1", GameShowTeams.B, t0),
            (3, "B2", GameShowTeams.B, t0.AddSeconds(1)));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, DateTime.UtcNow, session, preferredPlayerId: 1);
        GameShowEngine.ProcessAnswer(session, round, P(session, 1), "Frenos", DateTime.UtcNow);

        // Single player on A — stays active through strikes.
        GameShowEngine.ProcessAnswer(session, round, P(session, 1), "x1", DateTime.UtcNow);
        GameShowEngine.ProcessAnswer(session, round, P(session, 1), "x2", DateTime.UtcNow);
        var steal = GameShowEngine.ProcessAnswer(session, round, P(session, 1), "x3", DateTime.UtcNow);

        Assert.Equal(GameShowEngine.OutcomeKind.StealOpportunity, steal.Kind);
        Assert.Equal(GameShowRoundPhases.Steal, round.Phase);
        Assert.Equal(2, round.ActivePlayerId);

        var blocked = Assert.Throws<InvalidOperationException>(() =>
            GameShowEngine.ProcessAnswer(session, round, P(session, 3), "Espejos", DateTime.UtcNow));
        Assert.Equal("not_your_turn", blocked.Message);
    }

    [Fact]
    public void Roster_of_one_keeps_same_ActivePlayer_after_advance()
    {
        var t0 = DateTime.UtcNow;
        var session = SessionWithRoster(
            DefaultAnswers(),
            (1, "A1", GameShowTeams.A, t0),
            (2, "B1", GameShowTeams.B, t0));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, DateTime.UtcNow, session, preferredPlayerId: 1);

        GameShowEngine.ProcessAnswer(session, round, P(session, 1), "Frenos", DateTime.UtcNow);
        Assert.Equal(1, round.ActivePlayerId);

        GameShowEngine.ProcessAnswer(session, round, P(session, 1), "Luces", DateTime.UtcNow);
        Assert.Equal(1, round.ActivePlayerId);

        GameShowEngine.AdvanceActivePlayer(session, round);
        Assert.Equal(1, round.ActivePlayerId);
    }

    [Fact]
    public void AlreadyRevealed_does_not_rotate_ActivePlayer()
    {
        var t0 = DateTime.UtcNow;
        var session = SessionWithRoster(
            DefaultAnswers(),
            (1, "A1", GameShowTeams.A, t0),
            (2, "A2", GameShowTeams.A, t0.AddSeconds(1)),
            (3, "B1", GameShowTeams.B, t0));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, DateTime.UtcNow, session, preferredPlayerId: 1);
        GameShowEngine.ProcessAnswer(session, round, P(session, 1), "Frenos", DateTime.UtcNow);
        Assert.Equal(2, round.ActivePlayerId);

        var dup = GameShowEngine.ProcessAnswer(session, round, P(session, 2), "Frenos", DateTime.UtcNow);
        Assert.Equal(GameShowEngine.OutcomeKind.IgnoredAlreadyRevealed, dup.Kind);
        Assert.Equal(2, round.ActivePlayerId);
    }

    [Fact]
    public void FaceOff_miss_assigns_first_player_of_other_team()
    {
        var t0 = DateTime.UtcNow;
        var session = SessionWithRoster(
            DefaultAnswers(),
            (1, "A1", GameShowTeams.A, t0),
            (2, "B2", GameShowTeams.B, t0.AddSeconds(1)),
            (3, "B1", GameShowTeams.B, t0));
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, DateTime.UtcNow, session, preferredPlayerId: 1);

        GameShowEngine.ProcessAnswer(session, round, P(session, 1), "xxx", DateTime.UtcNow);

        Assert.Equal(GameShowRoundPhases.FaceOffSecond, round.Phase);
        // B1 joined earlier than B2 → first on roster.
        Assert.Equal(3, round.ActivePlayerId);
    }
}
