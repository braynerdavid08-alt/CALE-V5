using Cale.Modules.GameShow.Domain;

namespace Cale.Modules.GameShow.Application;

/// <summary>
/// Host corrections of the automatic matcher. Only the latest attempt of the current round can be
/// overruled, because only then the round state is still the direct consequence of that answer.
/// </summary>
public static class GameShowReviewEngine
{
    public enum AcceptMode
    {
        None,
        /// <summary>Wrong face-off answer: that team takes control with the chosen answer.</summary>
        FaceOff,
        /// <summary>Control answer that cost a strike (possibly the one that opened the steal).</summary>
        ControlStrike,
        /// <summary>Failed steal: the stealing team gets the bank instead of the controller.</summary>
        Steal
    }

    public static GameShowAttempt? LatestAttempt(GameShowRound round) =>
        round.Attempts
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

    public static AcceptMode GetAcceptMode(GameShowRound round, GameShowAttempt attempt, bool isCurrentRound)
    {
        if (!isCurrentRound || attempt.IsCorrect || LatestAttempt(round)?.Id != attempt.Id)
        {
            return AcceptMode.None;
        }

        // Repeats of an already revealed answer are logged without a strike: nothing to undo.
        if (round.Answers.Any(a => a.IsRevealed && GameShowAnswerMatcher.Matches(attempt.RawText, a.Text, a.AliasesJson))
            && round.Phase != GameShowRoundPhases.Finished)
        {
            return AcceptMode.None;
        }

        if (attempt.IsSteal)
        {
            return round.Phase == GameShowRoundPhases.Finished
                && !round.StealSucceeded
                && round.ControllingTeam is not null
                && !string.Equals(attempt.Team, round.ControllingTeam, StringComparison.OrdinalIgnoreCase)
                ? AcceptMode.Steal
                : AcceptMode.None;
        }

        if ((round.Phase == GameShowRoundPhases.FaceOffSecond || round.Phase == GameShowRoundPhases.WaitingBuzz)
            && !round.Answers.Any(a => a.IsRevealed))
        {
            return AcceptMode.FaceOff;
        }

        if ((GameShowRoundPhases.IsControl(round.Phase) || round.Phase == GameShowRoundPhases.Steal)
            && round.Strikes > 0
            && string.Equals(attempt.Team, round.ControllingTeam, StringComparison.OrdinalIgnoreCase))
        {
            return AcceptMode.ControlStrike;
        }

        return AcceptMode.None;
    }

    public static bool CanReject(GameShowRound round, GameShowAttempt attempt, bool isCurrentRound) =>
        isCurrentRound
        && attempt.IsCorrect
        && !attempt.IsSteal
        && attempt.MatchedAnswerId is not null
        && GameShowRoundPhases.IsControl(round.Phase)
        && string.Equals(attempt.Team, round.ControllingTeam, StringComparison.OrdinalIgnoreCase)
        && LatestAttempt(round)?.Id == attempt.Id;

    public static GameShowEngine.Outcome Accept(
        GameShowSession session,
        GameShowRound round,
        GameShowAttempt attempt,
        GameShowBoardAnswer answer,
        DateTime utcNow)
    {
        var mode = GetAcceptMode(round, attempt, isCurrentRound: true);
        var player = session.Players.FirstOrDefault(p => p.Id == attempt.PlayerId);
        var settings = GameShowSessionSettings.FromSession(session);

        switch (mode)
        {
            case AcceptMode.Steal:
            {
                var controller = round.ControllingTeam!;
                var banked = round.RoundPointsForController;
                var atAnswer = attempt.CreatedAt;
                AddScore(session, controller, -GameShowScoringPolicy.ApplyLightning(session, banked, atAnswer));
                var awarded = GameShowScoringPolicy.ComputeSuccessfulStealPoints(banked);
                AddScore(session, attempt.Team, GameShowScoringPolicy.ApplyLightning(session, awarded, atAnswer));
                round.StealSucceeded = true;
                MarkCorrect(attempt, answer);
                if (player is not null) GameShowEngine.NoteStealWon(player);
                RecomputeChampion(round);
                return new GameShowEngine.Outcome(
                    GameShowEngine.OutcomeKind.StealSucceeded,
                    "StealSucceeded",
                    new { team = attempt.Team, points = awarded, answerId = answer.Id, rank = answer.Rank, text = answer.Text, corrected = true });
            }

            case AcceptMode.FaceOff:
            {
                EnsureHidden(answer);
                round.Phase = GameShowRoundPhases.Control;
                round.ControllingTeam = attempt.Team;
                round.BuzzWinnerTeam = attempt.Team;
                round.Strikes = 0;
                round.StealSucceeded = false;
                GameShowEngine.RevealAnswer(answer, utcNow);
                round.RoundPointsForController = answer.Points;
                MarkCorrect(attempt, answer);
                if (player is not null) GameShowEngine.NoteCorrectAnswer(player);
                if (TryFinishIfBoardCleared(session, round, answer, utcNow) is { } won) return won;
                GameShowTiming.SetDeadline(round, utcNow, settings);
                GameShowEngine.AssignActivePlayer(session, round, attempt.Team, attempt.PlayerId);
                GameShowEngine.AdvanceActivePlayer(session, round);
                return new GameShowEngine.Outcome(
                    GameShowEngine.OutcomeKind.FaceOffWonControl,
                    "FaceOffWon",
                    new { team = attempt.Team, answerId = answer.Id, rank = answer.Rank, text = answer.Text, points = answer.Points, roundPoints = round.RoundPointsForController, activePlayerId = round.ActivePlayerId, corrected = true });
            }

            case AcceptMode.ControlStrike:
            {
                EnsureHidden(answer);
                var wasSteal = round.Phase == GameShowRoundPhases.Steal;
                round.Strikes = Math.Max(0, round.Strikes - 1);
                round.Phase = GameShowRoundPhases.Control;
                GameShowEngine.RevealAnswer(answer, utcNow);
                round.RoundPointsForController += answer.Points;
                MarkCorrect(attempt, answer);
                if (player is not null) GameShowEngine.NoteCorrectAnswer(player);
                if (TryFinishIfBoardCleared(session, round, answer, utcNow) is { } won) return won;
                GameShowTiming.SetDeadline(round, utcNow, settings);
                if (wasSteal)
                {
                    GameShowEngine.AssignActivePlayer(session, round, round.ControllingTeam!, attempt.PlayerId);
                    GameShowEngine.AdvanceActivePlayer(session, round);
                }

                return new GameShowEngine.Outcome(
                    GameShowEngine.OutcomeKind.CorrectBanked,
                    "CorrectAnswer",
                    new { team = attempt.Team, answerId = answer.Id, rank = answer.Rank, text = answer.Text, points = answer.Points, roundPoints = round.RoundPointsForController, activePlayerId = round.ActivePlayerId, corrected = true });
            }

            default:
                throw new InvalidOperationException("cannot_accept");
        }
    }

    public static GameShowEngine.Outcome Reject(
        GameShowSession session,
        GameShowRound round,
        GameShowAttempt attempt,
        DateTime utcNow)
    {
        if (!CanReject(round, attempt, isCurrentRound: true))
        {
            throw new InvalidOperationException("cannot_reject");
        }

        var answer = round.Answers.FirstOrDefault(a => a.Id == attempt.MatchedAnswerId);
        if (answer is not null && answer.IsRevealed)
        {
            answer.IsRevealed = false;
            answer.RevealedAt = null;
            round.RoundPointsForController = Math.Max(0, round.RoundPointsForController - answer.Points);
        }

        var player = session.Players.FirstOrDefault(p => p.Id == attempt.PlayerId);
        if (player is not null && player.CorrectAnswers > 0) player.CorrectAnswers--;
        attempt.IsCorrect = false;
        attempt.MatchedAnswerId = null;
        return GameShowEngine.HostStrike(session, round, utcNow);
    }

    private static GameShowEngine.Outcome? TryFinishIfBoardCleared(
        GameShowSession session,
        GameShowRound round,
        GameShowBoardAnswer answer,
        DateTime utcNow)
    {
        if (!round.Answers.All(a => a.IsRevealed)) return null;
        GameShowEngine.CommitRoundPoints(session, round.ControllingTeam!, round.RoundPointsForController, utcNow);
        GameShowEngine.FinishRound(round, utcNow);
        return new GameShowEngine.Outcome(
            GameShowEngine.OutcomeKind.RoundCompleted,
            "RoundWon",
            new { team = round.ControllingTeam, points = round.RoundPointsForController, answerId = answer.Id, rank = answer.Rank, corrected = true });
    }

    private static void EnsureHidden(GameShowBoardAnswer answer)
    {
        if (answer.IsRevealed) throw new InvalidOperationException("answer_revealed");
    }

    private static void MarkCorrect(GameShowAttempt attempt, GameShowBoardAnswer answer)
    {
        attempt.IsCorrect = true;
        attempt.MatchedAnswerId = answer.Id;
    }

    private static void AddScore(GameShowSession session, string team, int delta)
    {
        if (string.Equals(team, GameShowTeams.A, StringComparison.OrdinalIgnoreCase))
        {
            session.TeamAScore = Math.Max(0, session.TeamAScore + delta);
        }
        else
        {
            session.TeamBScore = Math.Max(0, session.TeamBScore + delta);
        }
    }

    private static void RecomputeChampion(GameShowRound round)
    {
        var top = round.Attempts
            .Where(a => a.IsCorrect && a.PlayerId is int)
            .GroupBy(a => a.PlayerId!.Value)
            .Select(g => new { PlayerId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.PlayerId)
            .FirstOrDefault();
        round.RoundChampionPlayerId = top?.PlayerId;
        round.RoundChampionCorrectAnswers = top?.Count ?? 0;
    }
}
