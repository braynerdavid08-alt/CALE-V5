namespace Cale.Modules.TheoreticalTraining.Application.DTOs;

public sealed record TheoryTopicDto(
    int Id,
    string Name,
    string? Description,
    string Color,
    string Category,
    bool IsActive);

public sealed record SaveTheoryTopicRequest(
    string Name,
    string? Description,
    string Color,
    string Category,
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
    int RequiredWorkshopHours,
    int? TheoryExamId,
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
    decimal WorkshopHoursCompleted,
    decimal WorkshopHoursRequired,
    int PendingClasses,
    int Absences,
    int CurrentStreak,
    int BestStreak,
    string? NextAction,
    string? ReservationCountdownLabel,
    DateTime? ReservationOpensAt,
    bool CheckedInToday,
    IReadOnlyList<TheoryDailyTaskDto> TodayTasks,
    string? AttendanceDayType = null,
    PracticalEligibilityDto? PracticalEligibility = null);

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
    DateTime? AcceptedAt,
    PracticalEligibilityDto? PracticalEligibility = null);

public sealed record PracticalEligibilityDto(
    bool CanBookPractical,
    bool TheoryExamPassed,
    bool TheoryHoursComplete,
    bool WorkshopHoursComplete,
    decimal TheoryHoursCompleted,
    decimal TheoryHoursRequired,
    decimal WorkshopHoursCompleted,
    decimal WorkshopHoursRequired,
    string? BlockReason);

public sealed record PracticalVehicleDto(
    int Id,
    string Label,
    string? Plate,
    bool IsActive);

public sealed record SavePracticalVehicleRequest(
    string Label,
    string? Plate,
    bool IsActive);

public sealed record PracticalLessonAssignmentDto(
    int StudentUserId,
    string StudentName,
    string? LicenseCategory,
    int LessonNumber,
    int LessonsRequired,
    int ReservationId,
    string ReservationStatus);

public sealed record PracticalLessonSessionDto(
    int Id,
    string SessionDate,
    string StartTime,
    string EndTime,
    int InstructorUserId,
    string InstructorName,
    int VehicleId,
    string VehicleLabel,
    int Capacity,
    int ReservedCount,
    int AvailableSeats,
    string Status,
    string? Notes,
    string? BookingState,
    string? BookingMessage,
    int? MyReservationId,
    PracticalLessonAssignmentDto? Assignment = null);

public sealed record PracticalSchedulingStudentDto(
    int StudentUserId,
    string StudentName,
    string? LicenseCategories,
    int CompletedLessons,
    int RequiredLessons,
    int NextLessonNumber,
    bool IsEligible,
    string? BlockReason);

public sealed record QuickAssignPracticalRequest(
    DateOnly SessionDate,
    string StartTime,
    string EndTime,
    int InstructorUserId,
    int VehicleId,
    int StudentUserId);

public sealed record DuplicatePracticalWeekRequest(
    DateOnly WeekStart,
    int InstructorUserId,
    int VehicleId);

public sealed record CreatePracticalLessonRequest(
    DateOnly SessionDate,
    string StartTime,
    string EndTime,
    int InstructorUserId,
    int VehicleId,
    int? Capacity,
    string? Notes);

public sealed record PracticalStudentDashboardDto(
    PracticalEligibilityDto Eligibility,
    PracticalLessonSessionDto? NextLesson,
    IReadOnlyList<PracticalLessonSessionDto> UpcomingReservations,
    IReadOnlyList<PracticalLessonSessionDto> AvailableLessons,
    IReadOnlyList<PracticalInstructorOptionDto> AvailableInstructors);

public sealed record PracticalInstructorOptionDto(
    int InstructorUserId,
    string InstructorName,
    int AvailableLessonCount);

public sealed record TheoryExamOptionDto(int Id, string Name);

public sealed record PracticalAttendanceRowDto(
    int StudentUserId,
    string StudentName,
    string Status,
    int ReservationId);

public sealed record UpdateEnrollmentRequest(
    string Status,
    string? AttendanceDayType = null,
    string? AllowedStartTime = null,
    string? LicenseCategories = null);
