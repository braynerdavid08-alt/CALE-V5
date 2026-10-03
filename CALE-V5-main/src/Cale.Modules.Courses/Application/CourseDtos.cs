using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cale.Modules.Courses.Application;

/// <summary>Who is editing: admins manage platform courses, schools manage their own.</summary>
public sealed record CourseActor(int UserId, string Role, int? SchoolUserId);

// ── Editor ──────────────────────────────────────────────────────────────

public sealed record CourseSaveRequest(
    string Title,
    string? Description,
    string? Category,
    string? CoverUrl,
    bool IsPublished);

public sealed record LessonCreateRequest(string Title);

public sealed record LessonSaveRequest(
    string Title,
    string? Summary,
    int EstimatedMinutes,
    JsonElement? Content);

public sealed record LessonReorderRequest(IReadOnlyList<int> LessonIds);

public sealed record CourseManageItemDto(
    int Id,
    string Title,
    string? Description,
    string Category,
    string? CoverUrl,
    bool IsPublished,
    bool IsPlatform,
    bool CanEdit,
    int LessonCount,
    DateTime UpdatedAt);

public sealed record LessonManageDto(
    int Id,
    int Position,
    string Title,
    string? Summary,
    int EstimatedMinutes,
    JsonNode Content);

public sealed record CourseManageDetailDto(
    int Id,
    string Title,
    string? Description,
    string Category,
    string? CoverUrl,
    bool IsPublished,
    bool IsPlatform,
    bool CanEdit,
    IReadOnlyList<LessonManageDto> Lessons,
    IReadOnlyList<string> Categories);

public sealed record CourseStudentProgressDto(
    int StudentUserId,
    string StudentName,
    int CompletedLessons,
    int TotalLessons,
    int Percent,
    int AverageScore,
    DateTime? LastActivityAt);

public sealed record CourseProgressReportDto(
    int CourseId,
    string Title,
    int TotalLessons,
    IReadOnlyList<CourseStudentProgressDto> Students);

// ── Student ─────────────────────────────────────────────────────────────

public sealed record StudentCourseItemDto(
    int Id,
    string Title,
    string? Description,
    string Category,
    string? CoverUrl,
    bool IsPlatform,
    int TotalLessons,
    int CompletedLessons,
    int Percent,
    int? NextLessonId);

public sealed record StudentLessonItemDto(
    int Id,
    int Position,
    string Title,
    string? Summary,
    int EstimatedMinutes,
    bool Completed,
    int? Score);

public sealed record StudentCourseDetailDto(
    int Id,
    string Title,
    string? Description,
    string Category,
    string? CoverUrl,
    int TotalLessons,
    int CompletedLessons,
    int Percent,
    IReadOnlyList<StudentLessonItemDto> Lessons);

public sealed record StudentLessonDto(
    int Id,
    int CourseId,
    string CourseTitle,
    int Position,
    int TotalLessons,
    string Title,
    string? Summary,
    int EstimatedMinutes,
    JsonNode Content,
    int QuizCount,
    bool Completed,
    int? BestScore,
    int? PreviousLessonId,
    int? NextLessonId);

/// <summary><c>Option</c> for single-choice activities; <c>Answer</c> (array) for order, fill-in and classify.</summary>
public sealed record QuizCheckRequest(int BlockIndex, int Option, JsonElement? Answer = null);

public sealed record QuizCheckResult(bool Correct, int CorrectIndex, string? Explanation, JsonNode? Solution = null);

public sealed record LessonCompleteRequest(IReadOnlyDictionary<int, JsonElement>? Answers);

public sealed record LessonCompleteResult(
    int Score,
    int BestScore,
    int CompletedLessons,
    int TotalLessons,
    bool CourseCompleted,
    bool FirstCompletion,
    int? NextLessonId);
