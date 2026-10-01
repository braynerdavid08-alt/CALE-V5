using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryExamScheduleService
{
    public async Task<ExamWeekDto> GetWeekAsync(
        int schoolUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var start = from ?? MondayOf(TodayColombia());
        var (rangeFrom, rangeTo) = ClampRange(start, to ?? start.AddDays(6));
        var days = await BuildDaysAsync(schoolUserId, rangeFrom, rangeTo, ct);
        var hasTemplates = await _db.Set<TheoryExamScheduleTemplate>()
            .AnyAsync(x => x.SchoolUserId == schoolUserId && x.IsActive, ct);
        return new ExamWeekDto(D(rangeFrom), D(rangeTo), hasTemplates, days);
    }

    public async Task<ExamDayDto> GetDayAsync(int schoolUserId, DateOnly date, CancellationToken ct) =>
        (await BuildDaysAsync(schoolUserId, date, date, ct))[0];

    public async Task<ExamSlotDto> GetSlotAsync(int schoolUserId, DateOnly date, TimeOnly time, CancellationToken ct)
    {
        var day = await GetDayAsync(schoolUserId, date, ct);
        return day.Slots.FirstOrDefault(s => s.Time == T(time))
            ?? new ExamSlotDto(D(date), T(time), 0, 0, 0, TheoryExamSlotStatuses.Closed,
                ExamSlotPlanner.SourceNone, false, null, null, null, []);
    }

    private async Task<IReadOnlyList<ExamDayDto>> BuildDaysAsync(
        int schoolUserId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var (templates, overrides) = await LoadScheduleAsync(schoolUserId, from, to, ct);
        var plan = ExamSlotPlanner.Plan(from, to, templates, overrides);
        var bookings = await LiveBookings(schoolUserId)
            .AsNoTracking()
            .Where(x => x.ExamDate >= from && x.ExamDate <= to)
            .OrderBy(x => x.SeatNumber)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
        var names = await LoadUserNamesAsync(
            bookings.Where(b => b.StudentUserId is int).Select(b => b.StudentUserId!.Value),
            ct);

        var result = new List<ExamDayDto>();
        foreach (var day in plan)
        {
            var dayBookings = bookings.Where(b => b.ExamDate == day.Date).ToList();
            var slots = day.Slots
                .Select(slot => MapSlot(slot, dayBookings.Where(b => b.SlotTime == slot.Time).ToList(), names))
                .ToList();

            // Bookings left in an hour that no longer exists stay visible so nobody gets lost.
            foreach (var orphan in dayBookings
                .Where(b => day.Slots.All(s => s.Time != b.SlotTime))
                .GroupBy(b => b.SlotTime))
            {
                var list = orphan.ToList();
                slots.Add(new ExamSlotDto(
                    D(day.Date),
                    T(orphan.Key),
                    list.Count,
                    list.Count,
                    0,
                    TheoryExamSlotStatuses.Closed,
                    ExamSlotPlanner.SourceNone,
                    false,
                    null,
                    null,
                    null,
                    list.Select(b => MapBooking(b, names)).ToList()));
            }

            result.Add(new ExamDayDto(
                D(day.Date),
                day.IsClosed,
                day.Closure?.Id,
                day.Closure?.Note,
                slots.OrderBy(s => s.Time, StringComparer.Ordinal).ToList()));
        }

        return result;
    }

    private ExamSlotDto MapSlot(
        ExamSlotPlanner.EffectiveSlot slot,
        IReadOnlyList<TheoryExamAppointment> bookings,
        IReadOnlyDictionary<int, string> names)
    {
        var occupied = bookings.Count;
        var available = slot.IsClosed ? 0 : Math.Max(0, slot.Capacity - occupied);
        return new ExamSlotDto(
            D(slot.Date),
            T(slot.Time),
            slot.Capacity,
            occupied,
            available,
            ExamSlotPlanner.ResolveStatus(slot.Capacity, occupied, slot.IsClosed, IsPast(slot.Date, slot.Time)),
            slot.Source,
            slot.Override is not null,
            slot.Override?.Id,
            slot.TemplateId,
            slot.Override?.Note,
            bookings.Select(b => MapBooking(b, names)).ToList());
    }

    private static ExamBookingDto MapBooking(TheoryExamAppointment b, IReadOnlyDictionary<int, string> names)
    {
        var name = b.StudentUserId is int sid && names.TryGetValue(sid, out var n)
            ? n
            : string.IsNullOrWhiteSpace(b.StudentLabel) ? "Sin nombre" : b.StudentLabel!;
        return new ExamBookingDto(
            b.Id,
            b.StudentUserId,
            name,
            b.Status,
            b.NoShow,
            b.CheckedInAt,
            b.Notes,
            b.StudentUserId is int student && b.BookedByUserId == student,
            b.CreatedAt);
    }

    /// <summary>Date-only change of one hour: capacity, close it, or add an extraordinary hour.</summary>
    public async Task<ExamDayDto> SaveSlotOverrideAsync(
        int schoolUserId,
        int actorUserId,
        SaveExamSlotOverrideRequest request,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var date = request.Date;
        var time = ParseSlotTime(request.Time);
        if (date < TodayColombia())
        {
            throw new DomainException("No puedes cambiar horarios de días que ya pasaron.", 400, "exam_slot_past");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var now = _clock.UtcNow;
        var template = await _db.Set<TheoryExamScheduleTemplate>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.IsActive
                && x.DayOfWeek == (int)date.DayOfWeek
                && x.StartTime == time, ct);
        var existing = await _db.Set<TheoryExamScheduleOverride>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.Date == date
                && x.StartTime == time
                && !x.IsWholeDay, ct);
        var booked = await LiveBookings(schoolUserId)
            .Where(x => x.ExamDate == date && x.SlotTime == time)
            .ToListAsync(ct);
        var label = $"{D(date)} {T(time)}";

        if (request.IsClosed)
        {
            if (booked.Count > 0 && !request.CancelBookings)
            {
                throw new DomainException(
                    $"Hay {booked.Count} estudiante(s) agendado(s) en esa hora. Cancela sus citas para cerrarla.",
                    409,
                    "slot_has_bookings");
            }

            CancelBookingsForClosure(schoolUserId, actorUserId, booked, now);
            if (template is null)
            {
                if (existing is not null)
                {
                    _db.Remove(existing);
                }
            }
            else
            {
                existing = UpsertOverride(existing, schoolUserId, actorUserId, date, time, now);
                existing.IsClosed = true;
                existing.Capacity = null;
                existing.Note = note;
            }

            SchoolAudit.Add(_db, now, schoolUserId, actorUserId, SchoolAuditAreas.ExamSchedule,
                "slot_closed",
                $"Hora cerrada solo el {label}" + (booked.Count > 0 ? $" ({booked.Count} cita(s) canceladas)." : "."),
                entityType: "exam_override", newValue: "cerrado", reason: note);
        }
        else
        {
            var capacity = ValidateCapacity(request.Capacity ?? template?.Capacity ?? 1);
            if (booked.Count > capacity)
            {
                throw new DomainException(
                    $"Ya hay {booked.Count} estudiantes agendados. El cupo no puede ser menor que eso.",
                    409,
                    "capacity_below_bookings");
            }

            var previous = existing is null
                ? template is null ? "sin horario" : $"{template.Capacity} (normal)"
                : existing.IsClosed ? "cerrado" : $"{existing.Capacity ?? template?.Capacity ?? 1}";
            if (template is not null && capacity == template.Capacity && note is null)
            {
                if (existing is not null)
                {
                    _db.Remove(existing);
                }
            }
            else
            {
                existing = UpsertOverride(existing, schoolUserId, actorUserId, date, time, now);
                existing.IsClosed = false;
                existing.Capacity = capacity;
                existing.Note = note;
            }

            SchoolAudit.Add(_db, now, schoolUserId, actorUserId, SchoolAuditAreas.ExamSchedule,
                template is null ? "extra_slot_saved" : "slot_capacity_changed",
                template is null
                    ? $"Horario extra el {label} con {capacity} cupo(s)."
                    : $"Cupo del {label}: {capacity} (solo esa fecha).",
                entityType: "exam_override", oldValue: previous, newValue: capacity.ToString(), reason: note);
        }

        await SaveOverridesAsync(ct);
        if (request.IsClosed)
        {
            await NotifyClosureCancellationsAsync(booked, ct);
        }

        return await GetDayAsync(schoolUserId, date, ct);
    }

    /// <summary>Removes a date exception so the hour goes back to the recurring schedule.</summary>
    public async Task<ExamDayDto> DeleteOverrideAsync(
        int schoolUserId,
        int actorUserId,
        int overrideId,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var entity = await _db.Set<TheoryExamScheduleOverride>()
            .FirstOrDefaultAsync(x => x.Id == overrideId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Excepción no encontrada.", "override_not_found");

        if (!entity.IsWholeDay)
        {
            var template = await _db.Set<TheoryExamScheduleTemplate>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                    && x.IsActive
                    && x.DayOfWeek == (int)entity.Date.DayOfWeek
                    && x.StartTime == entity.StartTime, ct);
            var booked = await LiveBookings(schoolUserId)
                .CountAsync(x => x.ExamDate == entity.Date && x.SlotTime == entity.StartTime, ct);
            if (booked > 0 && template is null)
            {
                throw new DomainException(
                    $"Hay {booked} estudiante(s) agendado(s) en este horario extra. Cancela sus citas primero.",
                    409,
                    "slot_has_bookings");
            }

            if (template is not null && booked > template.Capacity)
            {
                throw new DomainException(
                    $"Hay {booked} estudiantes agendados y el horario normal tiene {template.Capacity} cupo(s). Cancela citas o deja el cupo especial.",
                    409,
                    "capacity_below_bookings");
            }
        }

        _db.Remove(entity);
        SchoolAudit.Add(_db, _clock.UtcNow, schoolUserId, actorUserId, SchoolAuditAreas.ExamSchedule,
            entity.IsWholeDay ? "day_reopened" : "override_removed",
            entity.IsWholeDay
                ? $"Día {D(entity.Date)} reabierto."
                : $"El {D(entity.Date)} {T(entity.StartTime)} vuelve al horario normal.",
            entityType: "exam_override", entityId: entity.Id,
            oldValue: entity.IsClosed ? "cerrado" : entity.Capacity?.ToString());
        await _db.SaveChangesAsync(ct);
        return await GetDayAsync(schoolUserId, entity.Date, ct);
    }

    public async Task<ExamDayDto> CloseDayAsync(
        int schoolUserId,
        int actorUserId,
        DateOnly date,
        CloseExamDayRequest request,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        if (date < TodayColombia())
        {
            throw new DomainException("No puedes cerrar un día que ya pasó.", 400, "exam_slot_past");
        }

        var booked = await LiveBookings(schoolUserId)
            .Where(x => x.ExamDate == date)
            .ToListAsync(ct);
        if (booked.Count > 0 && !request.CancelBookings)
        {
            throw new DomainException(
                $"Hay {booked.Count} estudiante(s) agendado(s) ese día. Cancela sus citas para cerrarlo.",
                409,
                "slot_has_bookings");
        }

        var now = _clock.UtcNow;
        CancelBookingsForClosure(schoolUserId, actorUserId, booked, now);
        var closure = await _db.Set<TheoryExamScheduleOverride>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.Date == date && x.IsWholeDay, ct);
        closure = UpsertOverride(closure, schoolUserId, actorUserId, date, TimeOnly.MinValue, now);
        closure.IsWholeDay = true;
        closure.IsClosed = true;
        closure.Capacity = null;
        closure.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        SchoolAudit.Add(_db, now, schoolUserId, actorUserId, SchoolAuditAreas.ExamSchedule,
            "day_closed",
            $"Día {D(date)} cerrado para exámenes" + (booked.Count > 0 ? $" ({booked.Count} cita(s) canceladas)." : "."),
            entityType: "exam_override", newValue: "cerrado", reason: closure.Note);
        await SaveOverridesAsync(ct);
        await NotifyClosureCancellationsAsync(booked, ct);
        return await GetDayAsync(schoolUserId, date, ct);
    }

    public async Task<ExamDayDto> ReopenDayAsync(
        int schoolUserId,
        int actorUserId,
        DateOnly date,
        CancellationToken ct)
    {
        var closure = await _db.Set<TheoryExamScheduleOverride>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.Date == date && x.IsWholeDay, ct);
        return closure is null
            ? await GetDayAsync(schoolUserId, date, ct)
            : await DeleteOverrideAsync(schoolUserId, actorUserId, closure.Id, ct);
    }

    private TheoryExamScheduleOverride UpsertOverride(
        TheoryExamScheduleOverride? existing,
        int schoolUserId,
        int actorUserId,
        DateOnly date,
        TimeOnly time,
        DateTime now)
    {
        if (existing is not null)
        {
            existing.UpdatedAt = now;
            return existing;
        }

        var created = new TheoryExamScheduleOverride
        {
            SchoolUserId = schoolUserId,
            Date = date,
            StartTime = time,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Set<TheoryExamScheduleOverride>().Add(created);
        return created;
    }

    private void CancelBookingsForClosure(
        int schoolUserId,
        int actorUserId,
        IReadOnlyList<TheoryExamAppointment> bookings,
        DateTime now)
    {
        foreach (var booking in bookings)
        {
            booking.Status = TheoryExamBookingStatuses.Cancelled;
            booking.CancelledAt = now;
            booking.CancelledByUserId = actorUserId;
            booking.UpdatedAt = now;
            SchoolAudit.Add(_db, now, schoolUserId, actorUserId, SchoolAuditAreas.ExamBooking,
                "booking_cancelled",
                $"Cita cancelada por cierre del horario {D(booking.ExamDate)} {T(booking.SlotTime)}.",
                entityType: "exam_booking", entityId: booking.Id, studentUserId: booking.StudentUserId,
                oldValue: TheoryExamBookingStatuses.Active, newValue: TheoryExamBookingStatuses.Cancelled);
        }
    }

    private async Task NotifyClosureCancellationsAsync(
        IReadOnlyList<TheoryExamAppointment> bookings,
        CancellationToken ct)
    {
        foreach (var booking in bookings.Where(b => b.StudentUserId is int))
        {
            await NotifySafeAsync(booking.StudentUserId!.Value, new NotificationDraft(
                "Tu examen teórico fue cancelado",
                $"La escuela cerró el horario del {Human(booking.ExamDate, booking.SlotTime)}. Agenda una nueva fecha.",
                NotificationTypes.TheoryClass,
                RelatedEntity: "theory_exam_appointment",
                RelatedId: booking.Id,
                Link: "/student/exam"), ct);
        }
    }

    private async Task SaveOverridesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            throw new DomainException(
                "Otra persona cambió este horario al mismo tiempo. Vuelve a intentarlo.",
                409,
                "exam_schedule_conflict");
        }
    }
}
