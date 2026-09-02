using Cale.Modules.TheoreticalTraining.Domain;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed class TheoryBookingPolicy
{
    public int MaxWeekdayClassesPerDay { get; init; } = TheoryAttendanceLimits.DefaultMaxWeekdayClassesPerDay;
    public int MaxSaturdayClassesPerDay { get; init; } = TheoryAttendanceLimits.DefaultMaxSaturdayClassesPerDay;
    public int MaxDailyTheoryMinutes { get; init; }
    public int WeekdayReservationOpenDaysBefore { get; init; } = 1;
    public int SaturdayReservationOpenDaysBefore { get; init; } = 2;
    public TimeOnly? BookingWindowStart { get; init; }
    public TimeOnly? BookingWindowEnd { get; init; }

    public static TheoryBookingPolicy From(TheoryTrainingSettings settings) => new()
    {
        MaxWeekdayClassesPerDay = settings.MaxWeekdayClassesPerDay,
        MaxSaturdayClassesPerDay = settings.MaxSaturdayClassesPerDay,
        MaxDailyTheoryMinutes = settings.MaxDailyTheoryMinutes,
        WeekdayReservationOpenDaysBefore = settings.WeekdayReservationOpenDaysBefore,
        SaturdayReservationOpenDaysBefore = settings.SaturdayReservationOpenDaysBefore,
        BookingWindowStart = settings.StudentBookingWindowStart,
        BookingWindowEnd = settings.StudentBookingWindowEnd
    };

    public int MaxClassesFor(bool isSaturdayGroup, bool isSaturdaySession) =>
        isSaturdayGroup && isSaturdaySession
            ? MaxSaturdayClassesPerDay
            : MaxWeekdayClassesPerDay;

    public bool HasClassLimit(bool isSaturdayGroup, bool isSaturdaySession) =>
        MaxClassesFor(isSaturdayGroup, isSaturdaySession) > 0;

    public bool HasDailyMinutesLimit => MaxDailyTheoryMinutes > 0;

    public bool HasBookingWindow =>
        BookingWindowStart is not null && BookingWindowEnd is not null;

    public bool IsWithinBookingWindow(TimeOnly localTime)
    {
        if (!HasBookingWindow)
        {
            return true;
        }

        var start = BookingWindowStart!.Value;
        var end = BookingWindowEnd!.Value;
        if (start <= end)
        {
            return localTime >= start && localTime <= end;
        }

        return localTime >= start || localTime <= end;
    }

    public static string Describe(TheoryTrainingSettings settings)
    {
        var policy = From(settings);
        var parts = new List<string>();

        if (policy.MaxWeekdayClassesPerDay == 0)
        {
            parts.Add("entre semana sin límite de clases por día");
        }
        else if (policy.MaxWeekdayClassesPerDay == 1)
        {
            parts.Add("entre semana: 1 clase por día");
        }
        else
        {
            parts.Add($"entre semana: hasta {policy.MaxWeekdayClassesPerDay} clases por día");
        }

        if (policy.MaxSaturdayClassesPerDay == 0)
        {
            parts.Add("sábados sin límite de clases");
        }
        else if (policy.MaxSaturdayClassesPerDay == 1)
        {
            parts.Add("sábados: 1 clase por día");
        }
        else
        {
            parts.Add($"sábados: hasta {policy.MaxSaturdayClassesPerDay} clases por día");
        }

        if (policy.MaxDailyTheoryMinutes > 0)
        {
            var hours = policy.MaxDailyTheoryMinutes / 60.0;
            parts.Add(hours >= 1 && policy.MaxDailyTheoryMinutes % 60 == 0
                ? $"máximo {hours:0} h de teoría por día"
                : $"máximo {policy.MaxDailyTheoryMinutes} min de teoría por día");
        }

        if (policy.HasBookingWindow)
        {
            parts.Add(
                $"reservas de {policy.BookingWindowStart:HH\\:mm} a {policy.BookingWindowEnd:HH\\:mm} (hora Colombia)");
        }
        else
        {
            parts.Add("reservas en cualquier horario del día");
        }

        if (!settings.WeekdaysEnabled)
        {
            parts.Add("clases entre semana desactivadas");
        }

        if (!settings.SaturdayEnabled)
        {
            parts.Add("clases los sábados desactivadas");
        }

        return string.Join(" · ", parts);
    }

    public static int ReservationOpenDaysBefore(TheoryTrainingSettings settings, DateOnly sessionDate) =>
        sessionDate.DayOfWeek == DayOfWeek.Saturday
            ? Math.Max(0, settings.SaturdayReservationOpenDaysBefore)
            : Math.Max(0, settings.WeekdayReservationOpenDaysBefore);

    public static int SessionDurationMinutes(TimeOnly start, TimeOnly end)
    {
        var minutes = (int)(end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes;
        return Math.Max(0, minutes);
    }

    public static TimeOnly? ParseOptionalTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeOnly.TryParse(value.Trim(), out var parsed) ? parsed : null;
    }

    public static string? FormatOptionalTime(TimeOnly? value) =>
        value?.ToString("HH:mm");
}
