namespace Cale.Modules.TheoreticalTraining.Application;

public static class ColombiaTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static TimeZoneInfo TimeZone => Zone;

    public static DateTime UtcNow => DateTime.UtcNow;

    public static DateTime NowInColombia() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateOnly TodayInColombia() =>
        DateOnly.FromDateTime(NowInColombia());

    public static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            Zone);
    }

    public static DateTime StartOfDayUtc(DateOnly date) =>
        ToUtc(date, TimeOnly.MinValue);

    public static (DateTime OpenUtc, DateTime CloseUtc) ComputeReservationWindow(
        DateOnly sessionDate,
        TimeOnly startTime,
        int closeMinutesBefore = 0,
        int? openDaysBefore = null)
    {
        var daysBefore = openDaysBefore ?? (sessionDate.DayOfWeek switch
        {
            DayOfWeek.Saturday => 2,
            DayOfWeek.Sunday => 1,
            _ => 1
        });
        var openDate = sessionDate.AddDays(-Math.Max(0, daysBefore));

        var openUtc = StartOfDayUtc(openDate);
        var startUtc = ToUtc(sessionDate, startTime);
        var closeUtc = startUtc.AddMinutes(-closeMinutesBefore);
        if (closeUtc < openUtc)
        {
            closeUtc = startUtc;
        }

        return (openUtc, closeUtc);
    }

    public static bool TimesOverlap(
        DateOnly dateA,
        TimeOnly startA,
        TimeOnly endA,
        DateOnly dateB,
        TimeOnly startB,
        TimeOnly endB) =>
        dateA == dateB && startA < endB && startB < endA;

    public static IReadOnlyList<TimeSlotDef> StandardTwoHourSlots { get; } =
        Enumerable.Range(0, 12)
            .Select(i =>
            {
                var startHour = i * 2;
                var start = new TimeOnly(startHour, 0);
                var end = startHour >= 22
                    ? new TimeOnly(23, 59)
                    : new TimeOnly(startHour + 2, 0).AddMinutes(-1);
                return new TimeSlotDef(start, end);
            })
            .ToList();

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "America/Bogota", "SA Pacific Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "America/Bogota",
            TimeSpan.FromHours(-5),
            "Colombia",
            "Colombia");
    }
}

public sealed record TimeSlotDef(TimeOnly Start, TimeOnly End);
