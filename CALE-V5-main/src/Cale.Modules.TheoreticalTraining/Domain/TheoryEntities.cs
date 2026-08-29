namespace Cale.Modules.TheoreticalTraining.Domain;

public sealed class TheoryTopic
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Color { get; set; } = "#3B82F6";
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
    public bool WeekdaysEnabled { get; set; } = true;
    public bool SaturdayEnabled { get; set; } = true;
    public bool NotifyReservationOpen { get; set; } = true;
    public bool NotifyClassReminder24h { get; set; } = true;
    public bool NotifyClassReminder1h { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
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
    /// <summary>Only slot start time the student may book (e.g. 08:00).</summary>
    public TimeOnly? AllowedStartTime { get; set; }
    /// <summary>License categories in progress, e.g. A2,B1 or A2,C1.</summary>
    public string? LicenseCategories { get; set; }
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
