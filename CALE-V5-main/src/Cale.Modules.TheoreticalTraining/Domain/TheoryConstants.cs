namespace Cale.Modules.TheoreticalTraining.Domain;

public static class StudentEnrollmentStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Active = "Active";
    public const string Suspended = "Suspended";
    public const string Withdrawn = "Withdrawn";
    public const string Completed = "Completed";

    public static readonly HashSet<string> CanReserve =
        new(StringComparer.OrdinalIgnoreCase) { Accepted, Active };

    /// <summary>EF-translatable list (do not use HashSet in IQueryable).</summary>
    public static readonly string[] CanReserveStatuses = [Accepted, Active];
}

public static class TheoryClassStatuses
{
    public const string Scheduled = "Scheduled";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class TheoryReservationStatuses
{
    public const string Reserved = "Reserved";
    public const string Confirmed = "Confirmed";
    public const string Attended = "Attended";
    public const string NoShow = "NoShow";
    public const string CancelledByStudent = "CancelledByStudent";
    public const string CancelledBySchool = "CancelledBySchool";
    public const string Rescheduled = "Rescheduled";

    public static readonly HashSet<string> OccupiesSeat = new(StringComparer.OrdinalIgnoreCase)
    {
        Reserved, Confirmed, Attended, NoShow
    };

    /// <summary>EF-translatable list (do not use HashSet in IQueryable).</summary>
    public static readonly string[] OccupiesSeatStatuses =
        [Reserved, Confirmed, Attended, NoShow];

    public static readonly HashSet<string> Active = new(StringComparer.OrdinalIgnoreCase)
    {
        Reserved, Confirmed
    };

    /// <summary>EF-translatable list (do not use HashSet in IQueryable).</summary>
    public static readonly string[] ActiveStatuses = [Reserved, Confirmed];
}

public static class TheoryAttendanceStatuses
{
    public const string Pending = "Pending";
    public const string Present = "Present";
    public const string Absent = "Absent";
    public const string Late = "Late";
}
