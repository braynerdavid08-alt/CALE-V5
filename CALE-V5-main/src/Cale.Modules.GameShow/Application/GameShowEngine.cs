using Cale.Modules.GameShow.Domain;

namespace Cale.Modules.GameShow.Application;

/// <summary>Centralized scoring / steal rules.</summary>
public static class GameShowScoringPolicy
{
    public const int MaxStrikes = 3;

    /// <summary>
    /// Successful steal awards only the banked round points (prompt §9).
    /// The newly revealed answer proves the steal; its points are not added again.
    /// </summary>
    public static int ComputeSuccessfulStealPoints(int controllerBankedPoints) =>
        Math.Max(0, controllerBankedPoints);

    public static string OppositeTeam(string team) =>
        string.Equals(team, GameShowTeams.A, StringComparison.OrdinalIgnoreCase)
            ? GameShowTeams.B
            : GameShowTeams.A;
}

/// <summary>
/// Pure round engine — face-off, control, strikes, steal, temporary bank.
/// Handler persists + broadcasts; this class owns transition rules.
/// </summary>
public static class GameShowEngine
{
    public enum OutcomeKind
    {
        IgnoredAlreadyRevealed,
        FaceOffMissPass,
        FaceOffBothMissReopen,
        FaceOffWonControl,
        CorrectBanked,
        Strike,
        StealOpportunity,
        StealSucceeded,
        StealFailed,
        RoundCompleted,
        BuzzWindowExtended,
        Noop
    }

    public sealed record Outcome(
        OutcomeKind Kind,
        string EventName,
        object Payload);

    public static void OpenBuzz(GameShowRound round, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        round.Phase = GameShowRoundPhases.WaitingBuzz;
        round.ControllingTeam = null;
        round.BuzzWinnerTeam = null;
        round.Strikes = 0;
        round.RoundPointsForController = 0;
        round.StealSucceeded = false;
        round.BuzzOpenedAt = now;
        round.FinishedAt = null;
        foreach (var a in round.Answers)
        {
            a.IsRevealed = false;
            a.RevealedAt = null;
        }

        // No turn clock while teams decide who presses RESPONDER.
        GameShowTiming.ClearDeadline(round);
    }

    public static void EnterFaceOff(GameShowRound round, string team, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        round.Phase = GameShowRoundPhases.FaceOff;
        round.BuzzWinnerTeam = team;
        round.ControllingTeam = team;
        round.Strikes = 0;
        round.RoundPointsForController = 0;
        round.StealSucceeded = false;
        GameShowTiming.SetDeadline(round, now);
    }

    public static void RevealAnswer(GameShowBoardAnswer answer, DateTime utcNow)
    {
        answer.IsRevealed = true;
        answer.RevealedAt = utcNow;
    }

    public static void FinishRound(GameShowRound round, DateTime utcNow)
    {
        round.Phase = GameShowRoundPhases.Finished;
        round.FinishedAt = utcNow;
        GameShowTiming.ClearDeadline(round);
        foreach (var a in round.Answers.Where(x => !x.IsRevealed))
        {
            a.IsRevealed = true;
            a.RevealedAt = utcNow;
        }
    }

    public static void CommitRoundPoints(GameShowSession session, string team, int points)
    {
        if (points <= 0) return;
        if (string.Equals(team, GameShowTeams.A, StringComparison.OrdinalIgnoreCase))
        {
            session.TeamAScore += points;
        }
        else
        {
            session.TeamBScore += points;
        }
    }

    public static GameShowBoardAnswer? FindUnrevealedMatch(
        GameShowRound round,
        string text) =>
        round.Answers.FirstOrDefault(a =>
            !a.IsRevealed && GameShowAnswerMatcher.Matches(text, a.Text, a.AliasesJson));

    public static GameShowBoardAnswer? FindRevealedMatch(
        GameShowRound round,
        string text) =>
        round.Answers.FirstOrDefault(a =>
            a.IsRevealed && GameShowAnswerMatcher.Matches(text, a.Text, a.AliasesJson));

    /// <summary>
    /// Process a player answer during FaceOff / FaceOffSecond / Control / Steal.
    /// Mutates session/round; caller persists.
    /// </summary>
    public static Outcome ProcessAnswer(
        GameShowSession session,
        GameShowRound round,
        GameShowPlayer player,
        string text,
        DateTime utcNow,
        bool repeatedDoesNotStrike = true)
    {
        var raw = (text ?? "").Trim();
        var clipped = raw[..Math.Min(raw.Length, 200)];

        if (round.Phase == GameShowRoundPhases.Steal)
        {
            return ProcessSteal(session, round, player, clipped, utcNow);
        }

        if (!GameShowRoundPhases.CanAnswerAsController(round.Phase))
        {
            throw new InvalidOperationException("invalid_phase");
        }

        if (!string.Equals(player.Team, round.ControllingTeam, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("not_your_turn");
        }

        // Already revealed — do not consume strike by default.
        if (FindRevealedMatch(round, clipped) is not null)
        {
            round.Attempts.Add(MakeAttempt(round, player, clipped, correct: false, steal: false, null, utcNow));
            return new Outcome(
                OutcomeKind.IgnoredAlreadyRevealed,
                "AlreadyRevealed",
                new { team = player.Team, text = clipped });
        }

        var match = FindUnrevealedMatch(round, clipped);
        round.Attempts.Add(MakeAttempt(round, player, clipped, match is not null, false, match?.Id, utcNow));

        if (GameShowRoundPhases.IsFaceOff(round.Phase))
        {
            return ProcessFaceOff(session, round, player, match, utcNow);
        }

        // Control (or legacy Playing)
        if (match is null)
        {
            if (repeatedDoesNotStrike && FindRevealedMatch(round, clipped) is not null)
            {
                return new Outcome(
                    OutcomeKind.IgnoredAlreadyRevealed,
                    "AlreadyRevealed",
                    new { team = player.Team, text = clipped });
            }

            round.Strikes = Math.Min(GameShowScoringPolicy.MaxStrikes, round.Strikes + 1);
            if (round.Strikes >= GameShowScoringPolicy.MaxStrikes)
            {
                round.Phase = GameShowRoundPhases.Steal;
                GameShowTiming.SetDeadline(round, utcNow);
                return new Outcome(
                    OutcomeKind.StealOpportunity,
                    "StealOpportunity",
                    new { strikes = round.Strikes, stealer = GameShowScoringPolicy.OppositeTeam(round.ControllingTeam!) });
            }

            GameShowTiming.SetDeadline(round, utcNow);
            return new Outcome(
                OutcomeKind.Strike,
                "Strike",
                new { strikes = round.Strikes, text = clipped });
        }

        RevealAnswer(match, utcNow);
        round.RoundPointsForController += match.Points;

        if (round.Answers.All(a => a.IsRevealed))
        {
            CommitRoundPoints(session, round.ControllingTeam!, round.RoundPointsForController);
            FinishRound(round, utcNow);
            return new Outcome(
                OutcomeKind.RoundCompleted,
                "RoundWon",
                new
                {
                    team = round.ControllingTeam,
                    points = round.RoundPointsForController,
                    answerId = match.Id,
                    rank = match.Rank,
                    text = match.Text
                });
        }

        GameShowTiming.SetDeadline(round, utcNow);
        return new Outcome(
            OutcomeKind.CorrectBanked,
            "CorrectAnswer",
            new
            {
                team = player.Team,
                answerId = match.Id,
                rank = match.Rank,
                text = match.Text,
                points = match.Points,
                roundPoints = round.RoundPointsForController
            });
    }

    public static Outcome HostStrike(GameShowRound round, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        if (!GameShowRoundPhases.IsControl(round.Phase))
        {
            throw new InvalidOperationException("invalid_phase");
        }

        round.Strikes = Math.Min(GameShowScoringPolicy.MaxStrikes, round.Strikes + 1);
        if (round.Strikes >= GameShowScoringPolicy.MaxStrikes)
        {
            round.Phase = GameShowRoundPhases.Steal;
            GameShowTiming.SetDeadline(round, now);
            return new Outcome(
                OutcomeKind.StealOpportunity,
                "StealOpportunity",
                new { strikes = round.Strikes, forced = true });
        }

        GameShowTiming.SetDeadline(round, now);
        return new Outcome(OutcomeKind.Strike, "Strike", new { strikes = round.Strikes, forced = true });
    }

    public static Outcome HostFailSteal(GameShowSession session, GameShowRound round, DateTime utcNow)
    {
        if (round.Phase != GameShowRoundPhases.Steal)
        {
            throw new InvalidOperationException("invalid_phase");
        }

        if (round.ControllingTeam is string controller)
        {
            CommitRoundPoints(session, controller, round.RoundPointsForController);
        }

        FinishRound(round, utcNow);
        return new Outcome(
            OutcomeKind.StealFailed,
            "StealFailed",
            new { forced = true, points = round.RoundPointsForController, team = round.ControllingTeam });
    }

    public static Outcome HostEndRound(GameShowSession session, GameShowRound round, DateTime utcNow)
    {
        if (round.Phase == GameShowRoundPhases.Finished)
        {
            return new Outcome(OutcomeKind.RoundCompleted, "RoundEnded", new { forced = true, noop = true });
        }

        // During face-off, "end round" from host means reopen the buzz — do not finish the round.
        if (GameShowRoundPhases.IsFaceOff(round.Phase))
        {
            OpenBuzz(round, utcNow);
            return new Outcome(
                OutcomeKind.FaceOffBothMissReopen,
                "FaceOffReopen",
                new { forced = true });
        }

        // Keep banked points with current controller if any (or discard if still waiting buzz).
        if (round.ControllingTeam is string team
            && GameShowRoundPhases.IsControl(round.Phase)
            && round.RoundPointsForController > 0)
        {
            CommitRoundPoints(session, team, round.RoundPointsForController);
        }
        else if (round.Phase == GameShowRoundPhases.Steal
                 && round.ControllingTeam is string ctrl)
        {
            CommitRoundPoints(session, ctrl, round.RoundPointsForController);
        }

        FinishRound(round, utcNow);
        return new Outcome(OutcomeKind.RoundCompleted, "RoundEnded", new { forced = true });
    }

    /// <summary>Host reveal banks points only during Control; does not touch the scoreboard yet.</summary>
    public static Outcome? HostReveal(
        GameShowSession session,
        GameShowRound round,
        GameShowBoardAnswer answer,
        DateTime utcNow)
    {
        if (answer.IsRevealed) return null;

        RevealAnswer(answer, utcNow);

        if (GameShowRoundPhases.IsControl(round.Phase) && round.ControllingTeam is not null)
        {
            round.RoundPointsForController += answer.Points;
        }

        if (round.Answers.All(a => a.IsRevealed) && round.Phase != GameShowRoundPhases.Finished)
        {
            if (round.ControllingTeam is string team)
            {
                CommitRoundPoints(session, team, round.RoundPointsForController);
            }

            FinishRound(round, utcNow);
            return new Outcome(
                OutcomeKind.RoundCompleted,
                "RoundWon",
                new { team = round.ControllingTeam, points = round.RoundPointsForController, answerId = answer.Id });
        }

        return new Outcome(
            OutcomeKind.CorrectBanked,
            "AnswerRevealed",
            new { answerId = answer.Id, rank = answer.Rank, text = answer.Text, points = answer.Points });
    }

    /// <summary>
    /// Apply turn-clock expiry. Idempotent: returns Noop when deadline is null or still in the future.
    /// </summary>
    public static Outcome ProcessTimeout(GameShowSession session, GameShowRound round, DateTime utcNow)
    {
        if (!GameShowTiming.IsExpired(round, utcNow))
        {
            return new Outcome(OutcomeKind.Noop, "TimerTick", new { expired = false });
        }

        return round.Phase switch
        {
            GameShowRoundPhases.WaitingBuzz => new Outcome(
                OutcomeKind.Noop,
                "TimerTick",
                new { expired = false, phase = round.Phase }),
            GameShowRoundPhases.FaceOff => TimeoutFaceOffFirst(round, utcNow),
            GameShowRoundPhases.FaceOffSecond => TimeoutFaceOffSecond(round, utcNow),
            GameShowRoundPhases.Control or GameShowRoundPhases.Playing => TimeoutControl(round, utcNow),
            GameShowRoundPhases.Steal => HostFailSteal(session, round, utcNow),
            _ => new Outcome(OutcomeKind.Noop, "TimerTick", new { expired = true, phase = round.Phase })
        };
    }

    private static Outcome TimeoutFaceOffFirst(GameShowRound round, DateTime utcNow)
    {
        if (round.ControllingTeam is null)
        {
            OpenBuzz(round, utcNow);
            return new Outcome(OutcomeKind.FaceOffBothMissReopen, "FaceOffReopen", new { timedOut = true });
        }

        var other = GameShowScoringPolicy.OppositeTeam(round.ControllingTeam);
        round.Phase = GameShowRoundPhases.FaceOffSecond;
        round.ControllingTeam = other;
        GameShowTiming.SetDeadline(round, utcNow);
        return new Outcome(
            OutcomeKind.FaceOffMissPass,
            "FaceOffPass",
            new { fromTeam = GameShowScoringPolicy.OppositeTeam(other), toTeam = other, timedOut = true });
    }

    private static Outcome TimeoutFaceOffSecond(GameShowRound round, DateTime utcNow)
    {
        OpenBuzz(round, utcNow);
        return new Outcome(
            OutcomeKind.FaceOffBothMissReopen,
            "FaceOffReopen",
            new { timedOut = true });
    }

    private static Outcome TimeoutControl(GameShowRound round, DateTime utcNow) =>
        HostStrike(round, utcNow);

    private static Outcome ProcessFaceOff(
        GameShowSession session,
        GameShowRound round,
        GameShowPlayer player,
        GameShowBoardAnswer? match,
        DateTime utcNow)
    {
        if (match is null)
        {
            if (round.Phase == GameShowRoundPhases.FaceOff)
            {
                var other = GameShowScoringPolicy.OppositeTeam(player.Team);
                round.Phase = GameShowRoundPhases.FaceOffSecond;
                round.ControllingTeam = other;
                GameShowTiming.SetDeadline(round, utcNow);
                return new Outcome(
                    OutcomeKind.FaceOffMissPass,
                    "FaceOffPass",
                    new { fromTeam = player.Team, toTeam = other });
            }

            // Both missed face-off → reopen buzz (prompt: config; we reopen).
            OpenBuzz(round, utcNow);
            return new Outcome(
                OutcomeKind.FaceOffBothMissReopen,
                "FaceOffReopen",
                new { });
        }

        RevealAnswer(match, utcNow);
        round.RoundPointsForController = match.Points;
        round.Phase = GameShowRoundPhases.Control;
        round.ControllingTeam = player.Team;
        round.BuzzWinnerTeam = player.Team;
        round.Strikes = 0;

        if (round.Answers.All(a => a.IsRevealed))
        {
            CommitRoundPoints(session, player.Team, round.RoundPointsForController);
            FinishRound(round, utcNow);
            return new Outcome(
                OutcomeKind.RoundCompleted,
                "RoundWon",
                new { team = player.Team, points = round.RoundPointsForController, answerId = match.Id });
        }

        GameShowTiming.SetDeadline(round, utcNow);
        return new Outcome(
            OutcomeKind.FaceOffWonControl,
            "FaceOffWon",
            new
            {
                team = player.Team,
                answerId = match.Id,
                rank = match.Rank,
                text = match.Text,
                points = match.Points,
                roundPoints = round.RoundPointsForController
            });
    }

    private static Outcome ProcessSteal(
        GameShowSession session,
        GameShowRound round,
        GameShowPlayer player,
        string clipped,
        DateTime utcNow)
    {
        if (round.ControllingTeam is null)
        {
            throw new InvalidOperationException("invalid_state");
        }

        var stealer = GameShowScoringPolicy.OppositeTeam(round.ControllingTeam);
        if (!string.Equals(player.Team, stealer, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("not_your_turn");
        }

        if (FindRevealedMatch(round, clipped) is not null)
        {
            round.Attempts.Add(MakeAttempt(round, player, clipped, false, true, null, utcNow));
            return new Outcome(
                OutcomeKind.IgnoredAlreadyRevealed,
                "AlreadyRevealed",
                new { team = player.Team, text = clipped, steal = true });
        }

        var match = FindUnrevealedMatch(round, clipped);
        round.Attempts.Add(MakeAttempt(round, player, clipped, match is not null, true, match?.Id, utcNow));

        if (match is null)
        {
            // Steal failed — original controller keeps banked points.
            CommitRoundPoints(session, round.ControllingTeam, round.RoundPointsForController);
            FinishRound(round, utcNow);
            return new Outcome(
                OutcomeKind.StealFailed,
                "StealFailed",
                new { team = round.ControllingTeam, points = round.RoundPointsForController });
        }

        RevealAnswer(match, utcNow);
        var awarded = GameShowScoringPolicy.ComputeSuccessfulStealPoints(round.RoundPointsForController);
        CommitRoundPoints(session, stealer, awarded);
        round.StealSucceeded = true;
        FinishRound(round, utcNow);
        return new Outcome(
            OutcomeKind.StealSucceeded,
            "StealSucceeded",
            new
            {
                team = stealer,
                points = awarded,
                answerId = match.Id,
                rank = match.Rank,
                text = match.Text
            });
    }

    private static GameShowAttempt MakeAttempt(
        GameShowRound round,
        GameShowPlayer player,
        string text,
        bool correct,
        bool steal,
        int? matchedId,
        DateTime utcNow) =>
        new()
        {
            RoundId = round.Id,
            PlayerId = player.Id,
            Team = player.Team,
            RawText = text,
            IsCorrect = correct,
            IsSteal = steal,
            MatchedAnswerId = matchedId,
            CreatedAt = utcNow
        };
}
