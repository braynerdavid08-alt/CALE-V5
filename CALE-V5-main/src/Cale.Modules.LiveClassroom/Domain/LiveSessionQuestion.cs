namespace Cale.Modules.LiveClassroom.Domain;

public sealed class LiveSessionQuestion
{
    public int Id { get; private set; }
    public int SessionId { get; private set; }
    public int QuestionId { get; private set; }
    public int SortOrder { get; private set; }
    public string SnapshotJson { get; private set; } = "{}";
    public string? Topic { get; private set; }
    public string? Difficulty { get; private set; }
    public bool IsSurprise { get; private set; }
    public List<LiveAnswer> Answers { get; private set; } = [];

    private LiveSessionQuestion()
    {
    }

    public static LiveSessionQuestion Create(
        int sessionId,
        int questionId,
        int order,
        string snapshotJson,
        string? topic,
        string? difficulty,
        bool isSurprise = false)
    {
        return new LiveSessionQuestion
        {
            SessionId = sessionId,
            QuestionId = questionId,
            SortOrder = order,
            SnapshotJson = snapshotJson,
            Topic = topic,
            Difficulty = difficulty,
            IsSurprise = isSurprise
        };
    }

    public void SetSortOrder(int order) => SortOrder = order;
}
