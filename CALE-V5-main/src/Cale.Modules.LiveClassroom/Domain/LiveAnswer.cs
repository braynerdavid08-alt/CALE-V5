namespace Cale.Modules.LiveClassroom.Domain;

public sealed class LiveAnswer
{
    public int Id { get; private set; }
    public int SessionQuestionId { get; private set; }
    public int ParticipantId { get; private set; }
    public int OptionId { get; private set; }
    public bool IsCorrect { get; private set; }
    public int AnsweredAtMs { get; private set; }
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
        DateTime utcNow)
    {
        return new LiveAnswer
        {
            SessionQuestionId = sessionQuestionId,
            ParticipantId = participantId,
            OptionId = optionId,
            IsCorrect = isCorrect,
            AnsweredAtMs = Math.Max(0, answeredAtMs),
            CreatedAt = utcNow
        };
    }
}
