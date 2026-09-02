namespace Cale.Modules.LiveClassroom.Domain;

public sealed class LiveSessionConfig
{
    public int QuestionCount { get; set; } = 10;
    public int SecondsPerQuestion { get; set; } = 30;
    public bool Randomize { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;
    public bool ShowRanking { get; set; }
    public bool AnonymousNames { get; set; }
    public string FeedbackTiming { get; set; } = "end"; // end | immediate
    public string? TopicFilter { get; set; }
    public string? DifficultyFilter { get; set; }
    public List<string> DifficultyFilters { get; set; } = [];
    public bool CaleStandardPreset { get; set; }
    public List<int> BankIds { get; set; } = [];
    public List<string> TopicFilters { get; set; } = [];
    public Dictionary<int, List<string>> BankTopicFilters { get; set; } = [];
    public Dictionary<int, int> BankQuestionQuotas { get; set; } = [];
    public int? PresentationId { get; set; }

    public static LiveSessionConfig CaleStandard() => new()
    {
        QuestionCount = 25,
        SecondsPerQuestion = 72, // ~30 min / 25
        Randomize = true,
        ShuffleOptions = true,
        ShowRanking = false,
        AnonymousNames = false,
        FeedbackTiming = "end",
        CaleStandardPreset = true
    };
}
