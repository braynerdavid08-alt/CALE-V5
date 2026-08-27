namespace Cale.Modules.LiveClassroom.Domain;

public sealed class LiveAnswer
{
    public int Id { get; private set; }
    public int SessionQuestionId { get; private set; }
    public int ParticipantId { get; private set; }
    public int OptionId { get; private set; }
    public bool IsCorrect { get; private set; }
    public int AnsweredAtMs { get; private set; }
    public int Points { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private LiveAnswer()
    {
    }

    public static LiveAnswer Create(
        int sessionQuestionId,
        int participantId,
        int optionId,
        bool isCorrect,
        int answeredAtMs,
        int points,
        DateTime utcNow)
    {
        return new LiveAnswer
        {
            SessionQuestionId = sessionQuestionId,
            ParticipantId = participantId,
            OptionId = optionId,
            IsCorrect = isCorrect,
            AnsweredAtMs = Math.Max(0, answeredAtMs),
            Points = Math.Max(0, points),
            CreatedAt = utcNow
        };
    }

    /// <summary>Kahoot-style: up to 1000 pts if correct, decaying with latency.</summary>
    public static int ComputePoints(bool isCorrect, int answeredAtMs, int secondsPerQuestion)
    {
        if (!isCorrect)
        {
            return 0;
        }

        var windowMs = Math.Max(1, secondsPerQuestion) * 1000.0;
        var ratio = Math.Clamp(1.0 - (answeredAtMs / windowMs), 0.15, 1.0);
        return (int)Math.Round(1000.0 * ratio);
    }
}
