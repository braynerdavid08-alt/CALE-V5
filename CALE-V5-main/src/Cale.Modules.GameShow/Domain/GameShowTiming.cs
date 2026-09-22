namespace Cale.Modules.GameShow.Domain;

/// <summary>Classroom-friendly turn clocks for 100 Estudiantes Dijeron.</summary>
public static class GameShowTiming
{
    public static readonly TimeSpan BuzzWindow = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan FaceOffAnswer = TimeSpan.FromSeconds(12);
    public static readonly TimeSpan ControlAnswer = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan StealAnswer = TimeSpan.FromSeconds(12);

    public static TimeSpan ForPhase(string? phase) => phase switch
    {
        GameShowRoundPhases.WaitingBuzz => BuzzWindow,
        GameShowRoundPhases.FaceOff or GameShowRoundPhases.FaceOffSecond => FaceOffAnswer,
        GameShowRoundPhases.Control or GameShowRoundPhases.Playing => ControlAnswer,
        GameShowRoundPhases.Steal => StealAnswer,
        _ => TimeSpan.Zero
    };

    public static void SetDeadline(GameShowRound round, DateTime utcNow)
    {
        var span = ForPhase(round.Phase);
        round.AnswerDeadlineUtc = span > TimeSpan.Zero ? utcNow.Add(span) : null;
    }

    public static void ClearDeadline(GameShowRound round) =>
        round.AnswerDeadlineUtc = null;

    public static bool IsExpired(GameShowRound round, DateTime utcNow) =>
        round.AnswerDeadlineUtc is DateTime deadline && utcNow >= deadline;
}
