namespace Cale.Modules.TheoreticalTraining.Application.DTOs;

public sealed record TheoryTopicDto(
    int Id,
    string Name,
    string? Description,
    string Color,
    bool IsActive);

public sealed record SaveTheoryTopicRequest(
    string Name,
    string? Description,
    string Color,
    bool IsActive);

public sealed record TheoryClassroomDto(
    int Id,
    string Name,
    string? Identifier,
    int Capacity,
    string? Location,
    bool IsActive);

public sealed record SaveTheoryClassroomRequest(
    string Name,
    string? Identifier,
    int Capacity,
    string? Location,
    bool IsActive);

public sealed record TheorySettingsDto(
    int DefaultDurationMinutes,
    int MinCancelHours,
    int ReservationCloseMinutesBefore,
    int RequiredTheoryHours,
    bool WeekdaysEnabled,
    bool SaturdayEnabled,
    bool NotifyReservationOpen = true,
    bool NotifyClassReminder24h = true,
    bool NotifyClassReminder1h = true);

public sealed record TheoryTimeSlotDto(
    string Label,
    string Start,
    string End);

public sealed record TheoryClassSessionDto(
    int Id,
    DateOnly SessionDate,
    string StartTime,
    string EndTime,
    int TopicId,
    string TopicName,
    string TopicColor,
    int ClassroomId,
    string ClassroomName,
    int Capacity,
    int ReservedCount,
    int AvailableSeats,
    string Status,
    int? InstructorUserId,
    string? InstructorName,
    string? Notes,
    DateTime ReservationOpenAt,
    DateTime ReservationCloseAt,
    string? BookingState,
    string? BookingMessage,
    int? MyReservationId,
    string? MyReservationStatus);

public sealed record CreateTheoryClassRequest(
    DateOnly SessionDate,
    string StartTime,
    string EndTime,
    int TopicId,
    int ClassroomId,
    int? Capacity,
    int? InstructorUserId,
    string? Notes);

public sealed record UpdateTheoryClassRequest(
    DateOnly SessionDate,
    string StartTime,
    string EndTime,
    int TopicId,
    int ClassroomId,
    int? Capacity,
    int? InstructorUserId,
    string? Notes);

public sealed record TheoryMonthScheduleDto(
    DateOnly MonthStart,
    DateOnly MonthEnd,
    IReadOnlyList<TheoryClassSessionDto> Sessions);

public sealed record TheoryWeekScheduleDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    IReadOnlyList<TheoryClassSessionDto> Sessions,
    IReadOnlyList<TheoryTimeSlotDto> TimeSlots,
    string? StudentAttendanceDayType = null);

public sealed record TheorySchoolDashboardDto(
    int ClassesToday,
    int StudentsReserved,
    int AvailableSeats,
    int AbsencesToday,
    int ScheduledClasses);

public sealed record TheoryStudentDashboardDto(
    TheoryClassSessionDto? NextClass,
    IReadOnlyList<TheoryClassSessionDto> UpcomingReservations,
    decimal ProgressPercent,
    decimal HoursCompleted,
    decimal HoursRequired,
    int PendingClasses,
    int Absences,
    int CurrentStreak,
    int BestStreak,
    string? NextAction,
    string? ReservationCountdownLabel,
    DateTime? ReservationOpensAt,
    bool CheckedInToday,
    IReadOnlyList<TheoryDailyTaskDto> TodayTasks,
    string? AttendanceDayType = null);

public sealed record TheoryDailyTaskDto(string Label, bool Done);

public sealed record MarkAttendanceRequest(
    int StudentUserId,
    string Status,
    string? Notes);

public sealed record MarkAttendanceBatchRequest(
    IReadOnlyList<MarkAttendanceRequest> Rows);

public sealed record AttendanceRowDto(
    int StudentUserId,
    string StudentName,
    string Status,
    int? ReservationId);

public sealed record EnrollmentDto(
    int Id,
    int StudentUserId,
    string StudentName,
    string StudentEmail,
    string Status,
    string? AttendanceDayType,
    string? AllowedStartTime,
    string? LicenseCategories,
    DateTime CreatedAt,
    DateTime? AcceptedAt);

public sealed record UpdateEnrollmentRequest(
    string Status,
    string? AttendanceDayType = null,
    string? AllowedStartTime = null,
    string? LicenseCategories = null);
