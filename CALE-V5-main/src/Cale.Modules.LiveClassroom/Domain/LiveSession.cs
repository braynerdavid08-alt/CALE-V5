namespace Cale.Modules.LiveClassroom.Domain;

public static class LiveSessionStatuses
{
    public const string Lobby = "Lobby";
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Ended = "Ended";
}

public static class LiveSessionModes
{
    public const string Exam = "Exam";
    public const string Competitive = "Competitive";
    public const string Pedagogical = "Pedagogical";
}

public sealed class LiveSession
{
    public int Id { get; private set; }
    public int HostUserId { get; private set; }
    public string Title { get; private set; } = "";
    public string JoinCode { get; private set; } = "";
    public string Status { get; private set; } = LiveSessionStatuses.Lobby;
    public string Mode { get; private set; } = LiveSessionModes.Exam;
    public int BankId { get; private set; }
    public string ConfigJson { get; private set; } = "{}";
    public int CurrentQuestionIndex { get; private set; } = -1;
    public bool RevealCorrect { get; private set; }
    public DateTime? QuestionOpenedAt { get; private set; }
    public DateTime? QuestionClosesAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }

    public List<LiveParticipant> Participants { get; private set; } = [];
    public List<LiveSessionQuestion> Questions { get; private set; } = [];

    private LiveSession()
    {
    }

    public static LiveSession Create(
        int hostUserId,
        string title,
        string joinCode,
        string mode,
        int bankId,
        string configJson,
        DateTime utcNow)
    {
        return new LiveSession
        {
            HostUserId = hostUserId,
            Title = string.IsNullOrWhiteSpace(title) ? "CALE Aula en Vivo" : title.Trim(),
            JoinCode = joinCode.Trim().ToUpperInvariant(),
            Status = LiveSessionStatuses.Lobby,
            Mode = NormalizeMode(mode),
            BankId = bankId,
            ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson,
            CreatedAt = utcNow
        };
    }

    public void AddParticipant(LiveParticipant participant) =>
        Participants.Add(participant);

    public void SetQuestions(IEnumerable<LiveSessionQuestion> questions)
    {
        Questions.Clear();
        Questions.AddRange(questions);
    }

    public void MarkRunning(DateTime utcNow)
    {
        Status = LiveSessionStatuses.Running;
        StartedAt ??= utcNow;
    }

    public void Pause() => Status = LiveSessionStatuses.Paused;

    public void Resume() => Status = LiveSessionStatuses.Running;

    public void OpenQuestion(int index, DateTime openedAt, DateTime? closesAt)
    {
        CurrentQuestionIndex = index;
        QuestionOpenedAt = openedAt;
        QuestionClosesAt = closesAt;
        RevealCorrect = false;
        Status = LiveSessionStatuses.Running;
    }

    public void CloseCurrentQuestion(DateTime utcNow) =>
        QuestionClosesAt = utcNow;

    public void SetReveal(bool reveal) => RevealCorrect = reveal;

    public void End(DateTime utcNow)
    {
        Status = LiveSessionStatuses.Ended;
        EndedAt = utcNow;
        QuestionClosesAt = utcNow;
    }

    public bool IsQuestionOpen(DateTime utcNow) =>
        Status == LiveSessionStatuses.Running
        && CurrentQuestionIndex >= 0
        && QuestionOpenedAt is not null
        && (QuestionClosesAt is null || QuestionClosesAt > utcNow);

    private static string NormalizeMode(string? mode) => mode switch
    {
        LiveSessionModes.Competitive => LiveSessionModes.Competitive,
        LiveSessionModes.Pedagogical => LiveSessionModes.Pedagogical,
        _ => LiveSessionModes.Exam
    };
}
