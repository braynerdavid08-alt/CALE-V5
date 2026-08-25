namespace Cale.Modules.Classroom.Application.DTOs;

public sealed record GroupDto(
    int Id,
    string Name,
    string Code,
    int? TeacherId,
    string? TeacherName,
    string? Description,
    DateTime? StartsOn,
    bool IsActive,
    int MemberCount);

public sealed record SaveGroupRequest(
    string Name,
    string? Description,
    DateTime? StartsOn,
    bool IsActive);

public sealed record JoinGroupRequest(string Code);

public sealed record AddMemberRequest(string Email);

public sealed record MemberDto(
    int UserId,
    string Name,
    string Email,
    string Status);

public sealed record AnnouncementDto(
    int Id,
    string Title,
    string Body,
    int AuthorId,
    string AuthorName,
    DateTime CreatedAt);

public sealed record SaveAnnouncementRequest(string Title, string Body);

public sealed record MaterialDto(
    int Id,
    string Module,
    string Title,
    string? Description,
    string Type,
    string? Url,
    string? TextContent,
    DateTime CreatedAt);

public sealed record SaveMaterialRequest(
    string Module,
    string Title,
    string? Description,
    string Type,
    string? Url,
    string? TextContent);

public sealed record ActivityDto(
    int Id,
    string Type,
    string Title,
    string Description,
    string? Instructions,
    DateTime? DueAt,
    decimal? MaxScore,
    string Status,
    decimal? MyScore,
    string? TeacherComment);

public sealed record SaveActivityRequest(
    string Type,
    string Title,
    string Description,
    string? Instructions,
    DateTime? DueAt,
    decimal? MaxScore);

public sealed record SubmitActivityRequest(string? Text, string? FileUrl);

public sealed record GradeSubmissionRequest(decimal Score, string? Comment);

public sealed record SubmissionDto(
    int Id,
    int ActivityId,
    int UserId,
    string UserName,
    string? Text,
    string? FileUrl,
    DateTime SubmittedAt,
    decimal? Score,
    string? TeacherComment,
    string Status);

public sealed record StudentDashboardDto(
    string Name,
    IReadOnlyList<GroupDto> Groups,
    IReadOnlyList<ActivityDto> PendingActivities,
    IReadOnlyList<AnnouncementDto> Announcements,
    int UnreadNotifications,
    decimal? BestPercent);

public sealed record TeacherDashboardDto(
    IReadOnlyList<GroupDto> Groups,
    IReadOnlyList<SubmissionDto> PendingGrades,
    IReadOnlyList<ResultHintDto> LowScores);

public sealed record ResultHintDto(
    int UserId,
    string UserName,
    decimal Percent,
    bool Passed);

public sealed record AdminDashboardDto(
    int Users,
    int Groups,
    int Attempts,
    int Questions,
    int PendingRatings);
