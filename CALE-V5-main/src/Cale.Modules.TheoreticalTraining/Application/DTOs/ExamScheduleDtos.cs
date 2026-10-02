namespace Cale.Modules.TheoreticalTraining.Application.DTOs;

public sealed record ExamTemplateDto(int Id, int DayOfWeek, string Time, int Capacity, bool IsActive);

public sealed record CreateExamTemplatesRequest(IReadOnlyList<int> DaysOfWeek, string Time, int? Capacity);

public sealed record UpdateExamTemplateRequest(string Time, int Capacity, bool IsActive);

public sealed record ExamBookingDto(
    int Id,
    int? StudentUserId,
    string StudentName,
    string Status,
    bool NoShow,
    DateTime? CheckedInAt,
    string? Notes,
    bool BookedByStudent,
    DateTime CreatedAt);

public sealed record ExamSlotDto(
    string Date,
    string Time,
    int Capacity,
    int Occupied,
    int Available,
    string Status,
    string Source,
    bool IsOverridden,
    int? OverrideId,
    int? TemplateId,
    string? Note,
    IReadOnlyList<ExamBookingDto> Bookings);

public sealed record ExamDayDto(
    string Date,
    bool IsClosed,
    int? ClosureOverrideId,
    string? ClosureNote,
    IReadOnlyList<ExamSlotDto> Slots);

public sealed record ExamWeekDto(string From, string To, bool HasTemplates, IReadOnlyList<ExamDayDto> Days);

public sealed record SaveExamSlotOverrideRequest(
    DateOnly Date,
    string Time,
    int? Capacity,
    bool IsClosed,
    string? Note,
    bool CancelBookings = false);

public sealed record CloseExamDayRequest(string? Note, bool CancelBookings = false);

public sealed record SchoolBookExamRequest(
    DateOnly Date,
    string Time,
    int? StudentUserId,
    string? StudentLabel,
    string? Notes);

public sealed record CancelExamBookingRequest(string? Reason);

public sealed record StudentBookExamRequest(DateOnly Date, string Time);

public sealed record StudentExamSlotDto(string Date, string Time, int Available, string Status);

public sealed record StudentExamDayDto(string Date, IReadOnlyList<StudentExamSlotDto> Slots);

public sealed record StudentExamBookingDto(
    int Id,
    string Date,
    string Time,
    string Status,
    bool CanCancel,
    string? CancelDeadline);

public sealed record StudentExamAvailabilityDto(
    string From,
    string To,
    bool CanBook,
    string? BlockReason,
    StudentExamBookingDto? MyBooking,
    int MinCancelHours,
    IReadOnlyList<StudentExamDayDto> Days);

public sealed record HoursLineDto(decimal Required, decimal Attended, decimal Adjusted, decimal Total, decimal Pending);

public sealed record HoursAdjustmentDto(
    int Id,
    string Category,
    decimal PreviousHours,
    decimal NewHours,
    decimal DeltaHours,
    string Reason,
    string? PerformedBy,
    DateTime CreatedAt);

public sealed record StudentHoursDto(
    int StudentUserId,
    string StudentName,
    HoursLineDto Theory,
    HoursLineDto Workshop,
    IReadOnlyList<HoursAdjustmentDto> History);

public sealed record AdjustStudentHoursRequest(string Category, decimal NewHours, string Reason);

public sealed record SchoolAuditEntryDto(
    long Id,
    string Area,
    string Action,
    string? Summary,
    string? OldValue,
    string? NewValue,
    string? Reason,
    int? StudentUserId,
    string? StudentName,
    string? ActorName,
    DateTime CreatedAt);

public sealed record AgendaStudentDto(int? StudentUserId, string Name, string Status);

public sealed record AgendaItemDto(
    string Kind,
    int Id,
    string Date,
    string StartTime,
    string? EndTime,
    string Title,
    string? InstructorName,
    string Status,
    int Capacity,
    int Occupied,
    int Available,
    IReadOnlyList<AgendaStudentDto> Students);

public sealed record SchoolAgendaDto(string From, string To, IReadOnlyList<AgendaItemDto> Items);
