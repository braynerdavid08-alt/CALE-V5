namespace Cale.Modules.TheoreticalTraining.Application.DTOs;

/// <summary>Unified student formation snapshot. Eligibility comes only from existing services.</summary>
public sealed record StudentFormationStatusDto(
    string CurrentStage,
    string CurrentStageLabel,
    string? NextRecommendedAction,
    string? NextRecommendedPath,
    IReadOnlyList<FormationStageDto> Stages,
    IReadOnlyList<FormationUpcomingDto> Upcoming,
    PracticalEligibilityDto? PracticalEligibility,
    decimal BalanceDue,
    bool HasActiveSchool);

public sealed record FormationStageDto(
    string Id,
    string Title,
    string Status,
    string? Detail,
    string? BlockReason,
    bool CanBook,
    string? CtaLabel,
    string? CtaPath,
    decimal? HoursCompleted = null,
    decimal? HoursRequired = null,
    int? LessonsCompleted = null,
    int? LessonsRequired = null);

public sealed record FormationUpcomingDto(
    string Kind,
    string Title,
    string Date,
    string StartTime,
    string? EndTime,
    string Path);

public static class FormationStageIds
{
    public const string Enrollment = "enrollment";
    public const string Theory = "theory";
    public const string TheoryExam = "theory_exam";
    public const string Practical = "practical";
    public const string PracticalExam = "practical_exam";
    public const string Completed = "completed";
}

public static class FormationStageStatuses
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string AvailableToBook = "available_to_book";
    public const string Reserved = "reserved";
    public const string Completed = "completed";
    public const string Blocked = "blocked";
    public const string NeedsAuthorization = "needs_authorization";
}

/// <summary>School operational alert produced by rules (no LLM).</summary>
public sealed record SchoolOpsAlertDto(
    string Code,
    string Priority,
    string Title,
    string Detail,
    string? RecommendedAction,
    string? ActionPath,
    int? CountA = null,
    int? CountB = null);

public sealed record SchoolOpsAlertSummaryDto(
    string GeneratedAt,
    IReadOnlyList<SchoolOpsAlertDto> Alerts);
