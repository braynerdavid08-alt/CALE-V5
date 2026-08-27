namespace Cale.Modules.LiveClassroom.Domain;

public sealed class LiveDoubt
{
    public int Id { get; private set; }
    public int SessionId { get; private set; }
    public int ParticipantId { get; private set; }
    public string Text { get; private set; } = "";
    public int VoteCount { get; private set; }
    public bool IsResolved { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public List<LiveDoubtVote> Votes { get; private set; } = [];

    private LiveDoubt()
    {
    }

    public static LiveDoubt Create(
        int sessionId,
        int participantId,
        string text,
        DateTime utcNow)
    {
        var cleaned = (text ?? "").Trim();
        if (cleaned.Length is < 3 or > 280)
        {
            throw new ArgumentException("invalid_doubt_text");
        }

        return new LiveDoubt
        {
            SessionId = sessionId,
            ParticipantId = participantId,
            Text = cleaned,
            VoteCount = 0,
            IsResolved = false,
            CreatedAt = utcNow
        };
    }

    public void AddVote() => VoteCount++;

    public void Resolve() => IsResolved = true;
}

public sealed class LiveDoubtVote
{
    public int Id { get; private set; }
    public int DoubtId { get; private set; }
    public int ParticipantId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private LiveDoubtVote()
    {
    }

    public static LiveDoubtVote Create(int doubtId, int participantId, DateTime utcNow) =>
        new()
        {
            DoubtId = doubtId,
            ParticipantId = participantId,
            CreatedAt = utcNow
        };
}
