using System.Globalization;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>
/// Recurring theory-exam schedule, date overrides, capacity and bookings.
/// Bookings are rows of <see cref="TheoryExamAppointment"/>; a slot is never stored as a booking.
/// </summary>
public sealed partial class TheoryExamScheduleService
{
    public const int MaxCapacity = 200;
    public const int StudentMinLeadMinutes = 120;
    public const int StudentMaxDaysAhead = 60;
    private const int MaxRangeDays = 62;
    private const int MaxBookingAttempts = 4;

    private static readonly TimeOnly EarliestSlot = new(5, 0);
    private static readonly TimeOnly LatestSlot = new(22, 0);
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly ISchoolMembershipGuard _membership;
    private readonly INotificationPublisher _notifications;
    private readonly IExamBookingEligibility _eligibility;

    public TheoryExamScheduleService(
        CaleDbContext db,
        IClock clock,
        ISchoolMembershipGuard membership,
        INotificationPublisher notifications,
        IExamBookingEligibility eligibility)
    {
        _db = db;
        _clock = clock;
        _membership = membership;
        _notifications = notifications;
        _eligibility = eligibility;
    }

    private DateTime NowColombia() =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc),
            ColombiaTime.TimeZone);

    private DateOnly TodayColombia() => DateOnly.FromDateTime(NowColombia());

    private bool IsPast(DateOnly date, TimeOnly time) =>
        ColombiaTime.ToUtc(date, time) <= _clock.UtcNow;

    private static string D(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string T(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Human(DateOnly date, TimeOnly time) =>
        $"{date.ToString("dddd d 'de' MMMM", Es)} a las {time.ToString("h:mm tt", Es)}";

    internal static TimeOnly ParseSlotTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !TimeOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var time))
        {
            throw new DomainException("La hora no es válida.", 400, "invalid_time");
        }

        time = new TimeOnly(time.Hour, time.Minute);
        if (time < EarliestSlot || time > LatestSlot)
        {
            throw new DomainException(
                "La hora del examen debe estar entre las 5:00 a. m. y las 10:00 p. m.",
                400,
                "invalid_time");
        }

        return time;
    }

    internal static int ValidateCapacity(int? capacity)
    {
        var value = capacity ?? 1;
        if (value < 1 || value > MaxCapacity)
        {
            throw new DomainException(
                $"El cupo debe ser un número entre 1 y {MaxCapacity}.",
                400,
                "capacity_invalid");
        }

        return value;
    }

    private static (DateOnly From, DateOnly To) ClampRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        if (to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            to = from.AddDays(MaxRangeDays);
        }

        return (from, to);
    }

    private static DateOnly MondayOf(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private IQueryable<TheoryExamAppointment> LiveBookings(int schoolUserId) =>
        _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.Status != TheoryExamBookingStatuses.Cancelled);

    private async Task<Dictionary<int, string>> LoadUserNamesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        return await _db.Set<User>()
            .AsNoTracking()
            .Where(u => list.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    private async Task<int> ResolveStudentSchoolAsync(int studentUserId, CancellationToken ct)
    {
        var schoolId = await _db.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == studentUserId)
            .Select(u => u.SchoolId)
            .FirstOrDefaultAsync(ct);
        return schoolId ?? throw new ForbiddenException(
            "Tu cuenta no está vinculada a una escuela.",
            "no_school");
    }

    private async Task NotifySafeAsync(int userId, NotificationDraft draft, CancellationToken ct)
    {
        try
        {
            await _notifications.NotifyUsersAsync([userId], draft, ct);
        }
        catch (Exception)
        {
            // Notifications are best effort; the booking change is already saved.
        }
    }

    /// <summary>
    /// Serializes bookings of one slot (and one student) on Postgres. SQLite serializes writers with
    /// BEGIN IMMEDIATE, and the unique seat index is the final guarantee on every engine.
    /// </summary>
    private async Task LockAsync(int schoolUserId, DateOnly date, TimeOnly time, int? studentUserId, CancellationToken ct)
    {
        if (!_db.Database.IsNpgsql())
        {
            return;
        }

        if (studentUserId is int sid)
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(@p0, @p1)",
                new object[] { schoolUserId, -sid },
                ct);
        }

        var slotKey = date.DayNumber * 1440 + time.Hour * 60 + time.Minute;
        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(@p0, @p1)",
            new object[] { schoolUserId, slotKey },
            ct);
    }

    private async Task<(IReadOnlyList<TheoryExamScheduleTemplate> Templates, IReadOnlyList<TheoryExamScheduleOverride> Overrides)>
        LoadScheduleAsync(int schoolUserId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var templates = await _db.Set<TheoryExamScheduleTemplate>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);
        var overrides = await _db.Set<TheoryExamScheduleOverride>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId && x.Date >= from && x.Date <= to)
            .ToListAsync(ct);
        return (templates, overrides);
    }

    private async Task<ExamSlotPlanner.EffectiveSlot?> ResolveSlotAsync(
        int schoolUserId,
        DateOnly date,
        TimeOnly time,
        CancellationToken ct)
    {
        var (templates, overrides) = await LoadScheduleAsync(schoolUserId, date, date, ct);
        var day = ExamSlotPlanner.PlanDay(date, templates, overrides);
        return day.Slots.FirstOrDefault(s => s.Time == time);
    }
}
