namespace Cale.Modules.TheoreticalTraining.Domain;

public sealed class TheoryTopic
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Color { get; set; } = "#3B82F6";
    /// <summary>Theory or Workshop (taller).</summary>
    public string Category { get; set; } = TheoryTopicCategories.Theory;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TheoryClassroom
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public string Name { get; set; } = "";
    public string? Identifier { get; set; }
    public int Capacity { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TheoryTrainingSettings
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public int DefaultDurationMinutes { get; set; } = 120;
    public int MinCancelHours { get; set; } = 2;
    public int ReservationCloseMinutesBefore { get; set; }
    public int RequiredTheoryHours { get; set; } = 20;
    public int RequiredWorkshopHours { get; set; } = 10;
    public int? TheoryExamId { get; set; }
    public bool WeekdaysEnabled { get; set; } = true;
    public bool SaturdayEnabled { get; set; } = true;
    public bool NotifyReservationOpen { get; set; } = true;
    public bool NotifyClassReminder24h { get; set; } = true;
    public bool NotifyClassReminder1h { get; set; } = true;
    public bool NotifyExamReminder24h { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}

public sealed class EnrollmentAuthorizationEvent
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public int StudentUserId { get; set; }
    public string AuthorizationType { get; set; } = "";
    public string Action { get; set; } = "";
    public int? PerformedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class TheoryClassSession
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public int TopicId { get; set; }
    public int ClassroomId { get; set; }
    public int? InstructorUserId { get; set; }
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int Capacity { get; set; }
    public string Status { get; set; } = TheoryClassStatuses.Scheduled;
    public DateTime ReservationOpenAt { get; set; }
    public DateTime ReservationCloseAt { get; set; }
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public int? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public TheoryTopic? Topic { get; set; }
    public TheoryClassroom? Classroom { get; set; }
}

public sealed class TheoryClassReservation
{
    public int Id { get; set; }
    public int ClassSessionId { get; set; }
    public int StudentUserId { get; set; }
    public string Status { get; set; } = TheoryReservationStatuses.Reserved;
    public DateTime ReservedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public TheoryClassSession? ClassSession { get; set; }
}

public sealed class TheoryAttendanceRecord
{
    public int Id { get; set; }
    public int ClassSessionId { get; set; }
    public int StudentUserId { get; set; }
    public string Status { get; set; } = TheoryAttendanceStatuses.Pending;
    public int? MarkedByUserId { get; set; }
    public DateTime? MarkedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public TheoryClassSession? ClassSession { get; set; }
}

public sealed class SchoolStudentEnrollment
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public int StudentUserId { get; set; }
    public string Status { get; set; } = StudentEnrollmentStatuses.Pending;
    /// <summary>Weekday or Saturday — must match the school's scheduling mode.</summary>
    public string? AttendanceDayType { get; set; }
    /// <summary>Legacy field — students may book any school session on their assigned day.</summary>
    public TimeOnly? AllowedStartTime { get; set; }
    /// <summary>License categories in progress, e.g. A2,B1 or A2,C1.</summary>
    public string? LicenseCategories { get; set; }
    /// <summary>School authorized this student for the theory exam calendar.</summary>
    public bool TheoryExamAuthorized { get; set; }
    public DateTime? TheoryExamAuthorizedAt { get; set; }
    /// <summary>School authorized this student for practical / driving lessons.</summary>
    public bool PracticalAuthorized { get; set; }
    public DateTime? PracticalAuthorizedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class StudentDailyCheckIn
{
    public int Id { get; set; }
    public int StudentUserId { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateTime CheckInAt { get; set; }
}

public sealed class PracticalVehicle
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public string Label { get; set; } = "";
    public string? Plate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PracticalLessonSession
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int InstructorUserId { get; set; }
    public int VehicleId { get; set; }
    public int Capacity { get; set; } = 1;
    public string Status { get; set; } = PracticalLessonStatuses.Scheduled;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public PracticalVehicle? Vehicle { get; set; }
}

public sealed class PracticalLessonReservation
{
    public int Id { get; set; }
    public int LessonSessionId { get; set; }
    public int StudentUserId { get; set; }
    public string Status { get; set; } = PracticalReservationStatuses.Reserved;
    public DateTime ReservedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public PracticalLessonSession? LessonSession { get; set; }
}

public sealed class SchoolApprenticeProfile
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public int StudentUserId { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public DateOnly? EnrollmentDate { get; set; }
    public string? EnrollmentMonth { get; set; }
    public int? OrderNumber { get; set; }
    public string? ScheduleSlot { get; set; }
    public string? ReceiptNumber { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? BalancePaymentAmount { get; set; }
    public decimal AccountsReceivable { get; set; }
    public DateOnly? BalancePaymentDate { get; set; }
    public string? BalancePaymentMethod { get; set; }
    public string? BalanceReceiptNumber { get; set; }
    public string? EnrollmentPin { get; set; }
    public bool RuntRegistered { get; set; }
    public bool IsEnrolled { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TheoryExamAppointment
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public DateOnly ExamDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public int? StudentUserId { get; set; }
    public string? StudentLabel { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
