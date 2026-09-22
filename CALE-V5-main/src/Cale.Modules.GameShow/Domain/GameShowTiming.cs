namespace Cale.Modules.GameShow.Domain;

/// <summary>Classroom-friendly turn clocks for 100 Estudiantes Dijeron.</summary>
public static class GameShowTiming
{
    /// <summary>Legacy defaults — prefer <see cref="GameShowSessionSettings"/> when available.</summary>
    public static readonly TimeSpan BuzzWindow = TimeSpan.Zero;
    public static readonly TimeSpan FaceOffAnswer = TimeSpan.FromSeconds(25);
    public static readonly TimeSpan ControlAnswer = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StealAnswer = TimeSpan.FromSeconds(25);

    public static TimeSpan ForPhase(string? phase, GameShowSessionSettings? settings = null)
    {
        var s = settings ?? GameShowSessionSettings.CreateDefaults();
        return phase switch
        {
            GameShowRoundPhases.WaitingBuzz => BuzzWindow,
            GameShowRoundPhases.FaceOff or GameShowRoundPhases.FaceOffSecond =>
                TimeSpan.FromSeconds(Math.Max(1, s.FaceOffSeconds)),
            GameShowRoundPhases.Control or GameShowRoundPhases.Playing =>
                TimeSpan.FromSeconds(Math.Max(1, s.ControlSeconds)),
            GameShowRoundPhases.Steal =>
                TimeSpan.FromSeconds(Math.Max(1, s.StealSeconds)),
            _ => TimeSpan.Zero
        };
    }

    public static void SetDeadline(
        GameShowRound round,
        DateTime utcNow,
        GameShowSessionSettings? settings = null)
    {
        var span = ForPhase(round.Phase, settings);
        round.AnswerDeadlineUtc = span > TimeSpan.Zero ? utcNow.Add(span) : null;
    }

    public static void ClearDeadline(GameShowRound round) =>
        round.AnswerDeadlineUtc = null;

    public static bool IsExpired(GameShowRound round, DateTime utcNow) =>
        round.AnswerDeadlineUtc is DateTime deadline && utcNow >= deadline;
}
