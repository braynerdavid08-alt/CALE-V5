namespace Cale.Modules.TheoreticalTraining.Domain;

public static class TheoryTopicCategories
{
    public const string Theory = "Theory";
    public const string Workshop = "Workshop";

    public static bool IsValid(string? value) =>
        value is Theory or Workshop;

    public static string FormatLabel(string? value) =>
        value switch
        {
            Theory => "Teoría",
            Workshop => "Taller",
            _ => "Teoría"
        };

    public static string InferFromName(string name)
    {
        if (name.Contains("taller", StringComparison.OrdinalIgnoreCase))
        {
            return Workshop;
        }

        return Theory;
    }
}

public static class PracticalLessonStatuses
{
    public const string Scheduled = "Scheduled";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class PracticalScheduleLimits
{
    public const int MaxDailyInstructorHours = 8;
}

/// <summary>Clases prácticas requeridas por categoría (Colombia CEA).</summary>
public static class PracticalLessonRequirements
{
    public const int A2 = 15;
    public const int B1 = 20;
    public const int C1 = 30;

    public static int GetRequired(string? licenseCategories)
    {
        if (string.IsNullOrWhiteSpace(licenseCategories))
        {
            return B1;
        }

        var parts = licenseCategories
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant())
            .ToList();

        if (parts.Contains(StudentLicenseCategories.C1))
        {
            return C1;
        }

        if (parts.Contains(StudentLicenseCategories.B1))
        {
            return B1;
        }

        if (parts.Contains(StudentLicenseCategories.A2))
        {
            return A2;
        }

        return B1;
    }

    public static string PrimaryCategory(string? licenseCategories)
    {
        if (string.IsNullOrWhiteSpace(licenseCategories))
        {
            return StudentLicenseCategories.B1;
        }

        var first = licenseCategories
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(first)
            ? StudentLicenseCategories.B1
            : first.ToUpperInvariant();
    }
}

public static class PracticalReservationStatuses
{
    public const string Reserved = "Reserved";
    public const string CancelledByStudent = "CancelledByStudent";
    public const string CancelledBySchool = "CancelledBySchool";
    public const string Attended = "Attended";
    public const string NoShow = "NoShow";

    public static readonly string[] ActiveStatuses = [Reserved];

    public static readonly string[] OccupiesSeatStatuses = [Reserved, Attended, NoShow];
}

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

    public static readonly string[] Presets =
        [A2, B1, C1, A2B1, A2C1];

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

public static class TheoryHourStandards
{
    /// <summary>Horas fijas de plataforma — no editables por la escuela.</summary>
    public const int DefaultTheoryHours = 20;
    public const int DefaultWorkshopHours = 5;

    public const int A2TheoryHours = 20;
    public const int A2WorkshopHours = 3;

    public const int B1TheoryHours = 20;
    public const int B1WorkshopHours = 5;

    public const int C1TheoryHours = 20;
    public const int C1WorkshopHours = 5;

    public const int A2B1TheoryHours = 20;
    public const int A2B1WorkshopHours = 5;

    public const int A2C1TheoryHours = 20;
    public const int A2C1WorkshopHours = 5;

    public static (int TheoryHours, int WorkshopHours) ForLicense(string? licenseCategories)
    {
        if (string.IsNullOrWhiteSpace(licenseCategories))
        {
            return (DefaultTheoryHours, DefaultWorkshopHours);
        }

        var trimmed = licenseCategories.Trim();
        if (trimmed.Equals(StudentLicenseCategories.A2, StringComparison.OrdinalIgnoreCase))
        {
            return (A2TheoryHours, A2WorkshopHours);
        }

        if (trimmed.Equals(StudentLicenseCategories.B1, StringComparison.OrdinalIgnoreCase))
        {
            return (B1TheoryHours, B1WorkshopHours);
        }

        if (trimmed.Equals(StudentLicenseCategories.C1, StringComparison.OrdinalIgnoreCase))
        {
            return (C1TheoryHours, C1WorkshopHours);
        }

        if (trimmed.Equals(StudentLicenseCategories.A2B1, StringComparison.OrdinalIgnoreCase))
        {
            return (A2B1TheoryHours, A2B1WorkshopHours);
        }

        if (trimmed.Equals(StudentLicenseCategories.A2C1, StringComparison.OrdinalIgnoreCase))
        {
            return (A2C1TheoryHours, A2C1WorkshopHours);
        }

        return (DefaultTheoryHours, DefaultWorkshopHours);
    }
}

public static class TheoryAttendanceLimits
{
    public const int DefaultMaxWeekdayClassesPerDay = 1;
    public const int DefaultMaxSaturdayClassesPerDay = 4;

    /// <summary>Legacy alias.</summary>
    public const int MaxSaturdayReservationsPerDay = DefaultMaxSaturdayClassesPerDay;
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

public static class EnrollmentAuthorizationTypes
{
    public const string TheoryExam = "theory_exam";
    public const string Practical = "practical";
}

public static class EnrollmentAuthorizationActions
{
    public const string Granted = "granted";
    public const string Revoked = "revoked";
}
