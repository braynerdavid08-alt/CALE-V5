namespace Cale.Modules.Assessment.Domain.Gamification;

/// <summary>Five questions per student per Colombia calendar day.</summary>
public sealed class DailyChallenge
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly ChallengeDate { get; set; }
    public string QuestionIdsJson { get; set; } = "[]";
    /// <summary>JSON map questionId → { optionId, correct }.</summary>
    public string AnswersJson { get; set; } = "{}";
    public int CorrectCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Leitner-style box for a question the student got wrong.</summary>
public sealed class MistakeReview
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int QuestionId { get; set; }
    public int Box { get; set; }
    public DateTime NextDueAt { get; set; }
    public DateTime LastWrongAt { get; set; }
    public bool Mastered { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UserAchievement
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Code { get; set; } = "";
    public DateTime EarnedAt { get; set; }
}

public sealed class GameResult
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Game { get; set; } = "";
    public int Score { get; set; }
    public int Correct { get; set; }
    public int Total { get; set; }
    public bool Won { get; set; }
    public int? OpponentId { get; set; }
    public DateTime PlayedAt { get; set; }
}

public sealed class PlayerProfile
{
    public int UserId { get; set; }
    public bool ShowInRanking { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}

public static class GameKinds
{
    public const string Signs = "signs";
    public const string Duel = "duel";
}
