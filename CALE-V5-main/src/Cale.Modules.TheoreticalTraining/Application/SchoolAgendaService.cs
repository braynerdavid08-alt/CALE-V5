using System.Globalization;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>Read-only weekly agenda of a school: theory classes, driving lessons and theory exams.</summary>
public sealed class SchoolAgendaService
{
    private const int MaxRangeDays = 31;

    private readonly CaleDbContext _db;
    private readonly TheoryExamScheduleService _exams;

    public SchoolAgendaService(CaleDbContext db, TheoryExamScheduleService exams)
    {
        _db = db;
        _exams = exams;
    }

    public async Task<SchoolAgendaDto> GetAsync(
        int schoolUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var start = from ?? MondayOf(ColombiaTime.TodayInColombia());
        var end = to ?? start.AddDays(6);
        if (end < start)
        {
            end = start;
        }

        if (end.DayNumber - start.DayNumber > MaxRangeDays)
        {
            end = start.AddDays(MaxRangeDays);
        }

        var items = new List<AgendaItemDto>();
        items.AddRange(await TheoryItemsAsync(schoolUserId, start, end, ct));
        items.AddRange(await PracticalItemsAsync(schoolUserId, start, end, ct));
        items.AddRange(await ExamItemsAsync(schoolUserId, start, end, ct));

        return new SchoolAgendaDto(
            D(start),
            D(end),
            items
                .OrderBy(x => x.Date, StringComparer.Ordinal)
                .ThenBy(x => x.StartTime, StringComparer.Ordinal)
                .ThenBy(x => x.Kind, StringComparer.Ordinal)
                .ToList());
    }

    private async Task<IReadOnlyList<AgendaItemDto>> TheoryItemsAsync(
        int schoolUserId,
        DateOnly start,
        DateOnly end,
        CancellationToken ct)
    {
        var sessions = await _db.Set<TheoryClassSession>()
            .AsNoTracking()
            .Include(x => x.Topic)
            .Include(x => x.Classroom)
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate >= start
                && x.SessionDate <= end)
            .ToListAsync(ct);
        if (sessions.Count == 0)
        {
            return [];
        }

        var ids = sessions.Select(x => x.Id).ToList();
        var reservations = await _db.Set<TheoryClassReservation>()
            .AsNoTracking()
            .Where(x => ids.Contains(x.ClassSessionId)
                && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync(ct);
        var names = await NamesAsync(
            reservations.Select(x => x.StudentUserId)
                .Concat(sessions.Where(x => x.InstructorUserId is int).Select(x => x.InstructorUserId!.Value)),
            ct);

        return sessions.Select(s =>
        {
            var seats = reservations.Where(r => r.ClassSessionId == s.Id).ToList();
            var title = s.Topic?.Name ?? "Clase teórica";
            if (s.Classroom is not null)
            {
                title += $" · {s.Classroom.Name}";
            }

            return new AgendaItemDto(
                "theory",
                s.Id,
                D(s.SessionDate),
                T(s.StartTime),
                T(s.EndTime),
                title,
                s.InstructorUserId is int i ? names.GetValueOrDefault(i) : null,
                s.Status,
                s.Capacity,
                seats.Count,
                Math.Max(0, s.Capacity - seats.Count),
                seats.Select(r => new AgendaStudentDto(
                    r.StudentUserId,
                    names.GetValueOrDefault(r.StudentUserId, $"Estudiante {r.StudentUserId}"),
                    r.Status)).ToList());
        }).ToList();
    }

    private async Task<IReadOnlyList<AgendaItemDto>> PracticalItemsAsync(
        int schoolUserId,
        DateOnly start,
        DateOnly end,
        CancellationToken ct)
    {
        var lessons = await _db.Set<PracticalLessonSession>()
            .AsNoTracking()
            .Include(x => x.Vehicle)
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate >= start
                && x.SessionDate <= end
                && x.Status != PracticalLessonStatuses.Cancelled)
            .ToListAsync(ct);
        if (lessons.Count == 0)
        {
            return [];
        }

        var ids = lessons.Select(x => x.Id).ToList();
        var reservations = await _db.Set<PracticalLessonReservation>()
            .AsNoTracking()
            .Where(x => ids.Contains(x.LessonSessionId)
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync(ct);
        var names = await NamesAsync(
            reservations.Select(x => x.StudentUserId).Concat(lessons.Select(x => x.InstructorUserId)),
            ct);

        return lessons.Select(l =>
        {
            var seats = reservations.Where(r => r.LessonSessionId == l.Id).ToList();
            var vehicle = l.Vehicle is null
                ? "Clase de manejo"
                : $"Clase de manejo · {l.Vehicle.Label}{(string.IsNullOrWhiteSpace(l.Vehicle.Plate) ? "" : $" ({l.Vehicle.Plate})")}";
            return new AgendaItemDto(
                "practical",
                l.Id,
                D(l.SessionDate),
                T(l.StartTime),
                T(l.EndTime),
                vehicle,
                names.GetValueOrDefault(l.InstructorUserId),
                l.Status,
                l.Capacity,
                seats.Count,
                Math.Max(0, l.Capacity - seats.Count),
                seats.Select(r => new AgendaStudentDto(
                    r.StudentUserId,
                    names.GetValueOrDefault(r.StudentUserId, $"Estudiante {r.StudentUserId}"),
                    r.Status)).ToList());
        }).ToList();
    }

    private async Task<IReadOnlyList<AgendaItemDto>> ExamItemsAsync(
        int schoolUserId,
        DateOnly start,
        DateOnly end,
        CancellationToken ct)
    {
        var week = await _exams.GetWeekAsync(schoolUserId, start, end, ct);
        return week.Days
            .SelectMany(day => day.Slots.Select(slot => new AgendaItemDto(
                "exam",
                0,
                slot.Date,
                slot.Time,
                null,
                "Examen teórico",
                null,
                slot.Status,
                slot.Capacity,
                slot.Occupied,
                slot.Available,
                slot.Bookings.Select(b => new AgendaStudentDto(b.StudentUserId, b.StudentName, b.Status)).ToList())))
            .ToList();
    }

    private async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        return await _db.Set<User>()
            .AsNoTracking()
            .Where(u => list.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static string D(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string T(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
