namespace Cale.Modules.TheoreticalTraining.Domain;

/// <summary>Recurring weekly theory-exam slot of a school (repeats every week until changed).</summary>
public sealed class TheoryExamScheduleTemplate
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }

    /// <summary><see cref="System.DayOfWeek"/> numbering: 0 = Sunday … 6 = Saturday.</summary>
    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }
    public int Capacity { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Date-specific change to the recurring schedule: close one hour, change its capacity,
/// add an extraordinary hour, or close the whole day (<see cref="IsWholeDay"/>, StartTime = 00:00).
/// </summary>
public sealed class TheoryExamScheduleOverride
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public bool IsWholeDay { get; set; }
    public bool IsClosed { get; set; }

    /// <summary>Null keeps the recurring capacity.</summary>
    public int? Capacity { get; set; }

    public string? Note { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Manual correction of a student's theory/workshop hours; added on top of attendance.</summary>
public sealed class TrainingHoursAdjustment
{
    public int Id { get; set; }
    public int SchoolUserId { get; set; }
    public int StudentUserId { get; set; }
    public string Category { get; set; } = TheoryTopicCategories.Theory;
    public decimal DeltaHours { get; set; }
    public decimal PreviousHours { get; set; }
    public decimal NewHours { get; set; }
    public string Reason { get; set; } = "";
    public int? PerformedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Append-only record of administrative changes made inside a school.</summary>
public sealed class SchoolAuditEntry
{
    public long Id { get; set; }
    public int SchoolUserId { get; set; }
    public int? ActorUserId { get; set; }
    public string Area { get; set; } = "";
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public int? StudentUserId { get; set; }
    public string? Summary { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class TheoryExamBookingStatuses
{
    public const string Active = "Active";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";
    public const string Completed = "Completed";
}

public static class TheoryExamSlotStatuses
{
    public const string Available = "Available";
    public const string Full = "Full";
    public const string Closed = "Closed";
    public const string Past = "Past";
}

public static class SchoolAuditAreas
{
    public const string ExamSchedule = "exam_schedule";
    public const string ExamBooking = "exam_booking";
    public const string StudentHours = "student_hours";
}
