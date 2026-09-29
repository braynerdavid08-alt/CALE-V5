namespace Cale.Api.Services.Play;

public sealed record PlayOptionDto(int Id, string Text, string? ImageUrl);

public sealed record PlayQuestionDto(
    int Id,
    string Text,
    string Type,
    string? ImageUrl,
    string? Topic,
    IReadOnlyList<PlayOptionDto> Options);

public sealed record AnsweredQuestionDto(
    int QuestionId,
    int? OptionId,
    bool Correct,
    int? CorrectOptionId,
    string? Explanation);

public sealed record StreakDto(int Current, int Best, bool ActiveToday);

public sealed record BadgeDto(
    string Code,
    string Title,
    string Description,
    string Icon,
    bool Earned,
    DateTime? EarnedAt,
    int Current,
    int Target);

public sealed record LevelDto(
    int Level,
    string Name,
    int Xp,
    int LevelStartXp,
    int? NextLevelXp,
    string? NextLevelName,
    int ProgressPercent);

public sealed record DailyChallengeDto(
    DateOnly Date,
    IReadOnlyList<PlayQuestionDto> Questions,
    IReadOnlyList<AnsweredQuestionDto> Answers,
    int CorrectCount,
    bool Completed,
    StreakDto Streak);

public sealed record PlayAnswerRequest(int QuestionId, int OptionId);

public sealed record DailyAnswerResultDto(
    bool Correct,
    int? CorrectOptionId,
    string? Explanation,
    bool Completed,
    int CorrectCount,
    int Total,
    StreakDto Streak,
    IReadOnlyList<BadgeDto> NewBadges);

public sealed record MistakesDto(
    int DueCount,
    int PendingCount,
    int MasteredCount,
    DateTime? NextDueAt,
    IReadOnlyList<PlayQuestionDto> Questions);

public sealed record MistakeAnswerResultDto(
    bool Correct,
    int? CorrectOptionId,
    string? Explanation,
    bool Mastered,
    int Box,
    IReadOnlyList<BadgeDto> NewBadges);

public sealed record ReadinessTopicDto(
    int BlockId,
    string Name,
    int Answered,
    int Correct,
    int Percent,
    string Level,
    bool LowData);

public sealed record ReadinessDto(
    int Overall,
    string Label,
    string Recommendation,
    int RecentAttemptsAverage,
    int AnsweredQuestions,
    IReadOnlyList<ReadinessTopicDto> Topics);

public sealed record AchievementsDto(
    LevelDto Level,
    IReadOnlyList<BadgeDto> Badges,
    IReadOnlyList<BadgeDto> NewBadges);

public sealed record SignDto(string Code, string Family, string Name, string ImageUrl);

public sealed record SignsResultRequest(int Correct, int Total);

public sealed record GameSavedDto(
    int Score,
    int Best,
    bool IsRecord,
    IReadOnlyList<BadgeDto> NewBadges);

public sealed record RankingEntryDto(int Position, int UserId, string DisplayName, int Xp, bool IsMe);

public sealed record RankingScopeDto(string Scope, string Label, int? GroupId);

public sealed record RankingDto(
    string Scope,
    string ScopeLabel,
    int? GroupId,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    IReadOnlyList<RankingEntryDto> Entries,
    int? MyPosition,
    int MyXp,
    int Participants,
    bool ShowInRanking,
    IReadOnlyList<RankingScopeDto> Scopes);

public sealed record RankingVisibilityRequest(bool ShowInRanking);

public sealed record PlaySummaryDto(
    string FirstName,
    StreakDto Streak,
    LevelDto Level,
    int DailyAnswered,
    int DailyTotal,
    bool DailyCompleted,
    int MistakesDue,
    int MistakesPending,
    int Readiness,
    string ReadinessLabel,
    string? WeakestTopic,
    int? WeeklyRank,
    int WeeklyXp,
    int EarnedBadges,
    int TotalBadges,
    decimal? LastPercent,
    bool? LastPassed,
    IReadOnlyList<BadgeDto> NewBadges);

public sealed record InactiveStudentDto(
    int UserId,
    string Name,
    string Email,
    DateTime? LastActivityAt,
    int? DaysInactive);

public sealed record RemindStudentsRequest(IReadOnlyList<int> UserIds);
