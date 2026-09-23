using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Domain;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowFunFeaturesTests
{
    private static GameShowSession Session(
        params (string text, int pts)[] answers)
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
            Id = 1,
            Title = "Fun",
            Status = GameShowSessionStatuses.Running,
            TeamAName = "A",
            TeamBName = "B",
            CurrentRoundIndex = 0,
            Rounds = [round],
            Players =
            [
                new GameShowPlayer { Id = 1, DisplayName = "Ana", Team = GameShowTeams.A, JoinedAt = DateTime.UtcNow },
                new GameShowPlayer { Id = 2, DisplayName = "Beto", Team = GameShowTeams.B, JoinedAt = DateTime.UtcNow }
            ]
        };
    }

    [Fact]
    public void Lightning_doubles_committed_bank()
    {
        var session = Session(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var now = DateTime.UtcNow;
        session.LightningUntilUtc = now.AddSeconds(30);
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, now, session, 1);

        foreach (var ans in new[] { "Frenos", "Luces", "Llantas", "Espejos", "Aceite" })
        {
            GameShowEngine.ProcessAnswer(session, round, session.Players[0], ans, now);
        }

        Assert.Equal(GameShowRoundPhases.Finished, round.Phase);
        Assert.Equal(200, session.TeamAScore); // 100 bank ×2
        Assert.True(session.Players[0].CorrectAnswers >= 5);
    }

    [Fact]
    public void Without_lightning_bank_commits_normally()
    {
        var session = Session(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var now = DateTime.UtcNow;
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, now, session, 1);

        foreach (var ans in new[] { "Frenos", "Luces", "Llantas", "Espejos", "Aceite" })
        {
            GameShowEngine.ProcessAnswer(session, round, session.Players[0], ans, now);
        }

        Assert.Equal(100, session.TeamAScore);
    }

    [Fact]
    public void Round_champion_is_player_with_most_corrects()
    {
        var session = Session(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        session.Players.Add(new GameShowPlayer
        {
            Id = 3,
            DisplayName = "Carla",
            Team = GameShowTeams.A,
            JoinedAt = DateTime.UtcNow.AddSeconds(1)
        });
        var now = DateTime.UtcNow;
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, now, session, 1);

        GameShowEngine.ProcessAnswer(session, round, session.Players[0], "Frenos", now); // Ana
        // Active rotates to Carla (id 3)
        Assert.Equal(3, round.ActivePlayerId);
        GameShowEngine.ProcessAnswer(session, round, session.Players[2], "Luces", now);
        GameShowEngine.ProcessAnswer(session, round, session.Players[0], "Llantas", now);
        GameShowEngine.ProcessAnswer(session, round, session.Players[2], "Espejos", now);
        GameShowEngine.ProcessAnswer(session, round, session.Players[0], "Aceite", now);

        Assert.Equal(GameShowRoundPhases.Finished, round.Phase);
        // Ana: Frenos, Llantas, Aceite = 3; Carla: Luces, Espejos = 2
        Assert.Equal(1, round.RoundChampionPlayerId);
        Assert.Equal(3, round.RoundChampionCorrectAnswers);
    }

    [Fact]
    public void Successful_steal_increments_StealsWon()
    {
        var session = Session(("Frenos", 30), ("Luces", 25), ("Llantas", 20), ("Espejos", 15), ("Aceite", 10));
        var now = DateTime.UtcNow;
        var round = session.Rounds[0];
        GameShowEngine.EnterFaceOff(round, GameShowTeams.A, now, session, 1);
        GameShowEngine.ProcessAnswer(session, round, session.Players[0], "Frenos", now);
        GameShowEngine.ProcessAnswer(session, round, session.Players[0], "x1", now);
        GameShowEngine.ProcessAnswer(session, round, session.Players[0], "x2", now);
        GameShowEngine.ProcessAnswer(session, round, session.Players[0], "x3", now);

        GameShowEngine.ProcessAnswer(session, round, session.Players[1], "Luces", now);

        Assert.Equal(1, session.Players[1].StealsWon);
        Assert.True(session.Players[1].CorrectAnswers >= 1);
    }

    [Fact]
    public void ApplyLightning_policy()
    {
        var session = new GameShowSession { LightningUntilUtc = DateTime.UtcNow.AddSeconds(10) };
        Assert.Equal(150, GameShowScoringPolicy.ApplyLightning(session, 75, DateTime.UtcNow));
        session.LightningUntilUtc = DateTime.UtcNow.AddSeconds(-1);
        Assert.Equal(75, GameShowScoringPolicy.ApplyLightning(session, 75, DateTime.UtcNow));
    }
}
