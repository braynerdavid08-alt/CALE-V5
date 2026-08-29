namespace Cale.Modules.TheoreticalTraining.Domain;

public static class StudentAttendanceDayTypes
{
    public const string Weekday = "Weekday";
    public const string Saturday = "Saturday";

    public static bool IsValid(string? value) =>
        value is Weekday or Saturday;

    public static string FormatLabel(string? value) =>
        value switch
        {
            Weekday => "Semana",
            Saturday => "Sábados",
            _ => "Sin asignar"
        };
}

public static class StudentLicenseCategories
{
    public const string A2 = "A2";
    public const string B1 = "B1";
    public const string C1 = "C1";
    public const string A2B1 = "A2,B1";
    public const string A2C1 = "A2,C1";
    public const string B1C1 = "B1,C1";
    public const string A2B1C1 = "A2,B1,C1";

    public static readonly string[] Presets =
        [A2, B1, C1, A2B1, A2C1, B1C1, A2B1C1];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Presets.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string FormatLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Sin asignar";
        }

        return string.Join(" + ", value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}

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
