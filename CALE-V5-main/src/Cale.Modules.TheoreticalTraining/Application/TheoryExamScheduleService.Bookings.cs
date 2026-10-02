using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryExamScheduleService
{
    private sealed record BookingRequest(
        int SchoolUserId,
        DateOnly Date,
        TimeOnly Time,
        int? StudentUserId,
        string? StudentLabel,
        string? Notes,
        int ActorUserId,
        bool ByStudent,
        bool GrowCapacityIfNeeded);

    public async Task<ExamSlotDto> BookAsSchoolAsync(
        int schoolUserId,
        int actorUserId,
        SchoolBookExamRequest request,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var time = ParseSlotTime(request.Time);
        var label = string.IsNullOrWhiteSpace(request.StudentLabel) ? null : request.StudentLabel.Trim();
        if (request.StudentUserId is null && label is null)
        {
            throw new DomainException("Elige el estudiante que va a presentar el examen.", 400, "student_required");
        }

        if (IsPast(request.Date, time))
        {
            throw new DomainException("Ese horario ya pasó.", 400, "exam_slot_past");
        }

        if (request.StudentUserId is int studentId)
        {
            await _eligibility.EnsureEligibleAsync(schoolUserId, studentId, ct);
            await EnsureNoOtherUpcomingBookingAsync(schoolUserId, studentId, null, ct);
        }

        var booking = await BookCoreAsync(new BookingRequest(
            schoolUserId,
            request.Date,
            time,
            request.StudentUserId,
            label,
            request.Notes,
            actorUserId,
            ByStudent: false,
            GrowCapacityIfNeeded: false), ct);

        if (booking.StudentUserId is int notifyId)
        {
            await NotifySafeAsync(notifyId, new NotificationDraft(
                "Cita de examen teórico",
                $"Tu examen teórico quedó agendado para el {Human(booking.ExamDate, booking.SlotTime)}.",
                NotificationTypes.TheoryClass,
                RelatedEntity: "theory_exam_appointment",
                RelatedId: booking.Id,
                Link: "/student/exam"), ct);
        }

        return await GetSlotAsync(schoolUserId, booking.ExamDate, booking.SlotTime, ct);
    }

    public async Task<ExamSlotDto> CancelAsSchoolAsync(
        int schoolUserId,
        int actorUserId,
        int bookingId,
        string? reason,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var booking = await _db.Set<TheoryExamAppointment>()
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Cita no encontrada.", "booking_not_found");

        if (booking.Status != TheoryExamBookingStatuses.Cancelled)
        {
            var cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            MarkCancelled(booking, actorUserId);
            SchoolAudit.Add(_db, _clock.UtcNow, schoolUserId, actorUserId, SchoolAuditAreas.ExamBooking,
                "booking_cancelled",
                $"Cita de examen cancelada por la escuela ({D(booking.ExamDate)} {T(booking.SlotTime)}).",
                entityType: "exam_booking", entityId: booking.Id, studentUserId: booking.StudentUserId,
                oldValue: TheoryExamBookingStatuses.Active, newValue: TheoryExamBookingStatuses.Cancelled,
                reason: cleanReason);
            await _db.SaveChangesAsync(ct);

            if (booking.StudentUserId is int studentId && !IsPast(booking.ExamDate, booking.SlotTime))
            {
                await NotifySafeAsync(studentId, new NotificationDraft(
                    "Tu examen teórico fue cancelado",
                    $"La escuela canceló tu cita del {Human(booking.ExamDate, booking.SlotTime)}."
                        + (cleanReason is null ? "" : $" Motivo: {cleanReason}"),
                    NotificationTypes.TheoryClass,
                    RelatedEntity: "theory_exam_appointment",
                    RelatedId: booking.Id,
                    Link: "/student/exam"), ct);
            }
        }

        return await GetSlotAsync(schoolUserId, booking.ExamDate, booking.SlotTime, ct);
    }

    /// <summary>
    /// Excel import: keeps existing behaviour (one row per student per hour) but respects seats.
    /// If the hour has no room, the date's capacity grows so imported data is never lost.
    /// </summary>
    public async Task<TheoryExamAppointment> ImportBookingAsync(
        int schoolUserId,
        DateOnly date,
        TimeOnly time,
        int? studentUserId,
        string label,
        CancellationToken ct)
    {
        var existing = await LiveBookings(schoolUserId)
            .Where(x => x.ExamDate == date && x.SlotTime == time)
            .ToListAsync(ct);
        var match = existing.FirstOrDefault(x => studentUserId is int sid
                ? x.StudentUserId == sid
                : x.StudentUserId == null && string.Equals(x.StudentLabel, label, StringComparison.OrdinalIgnoreCase))
            ?? existing.FirstOrDefault(x => x.StudentUserId == null
                && string.Equals(x.StudentLabel, label, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            match.StudentUserId = studentUserId;
            match.StudentLabel = label;
            match.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            return match;
        }

        return await BookCoreAsync(new BookingRequest(
            schoolUserId, date, time, studentUserId, label, null, schoolUserId,
            ByStudent: false, GrowCapacityIfNeeded: true), ct);
    }

    /// <summary>Legacy PUT schedule/{id}: change student/notes in place, or move the booking to another hour.</summary>
    public async Task<TheoryExamAppointment> UpdateBookingAsync(
        int schoolUserId,
        int actorUserId,
        int bookingId,
        DateOnly date,
        string slotTime,
        int? studentUserId,
        string? studentLabel,
        string? notes,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var booking = await _db.Set<TheoryExamAppointment>()
            .FirstOrDefaultAsync(x => x.Id == bookingId
                && x.SchoolUserId == schoolUserId
                && x.Status != TheoryExamBookingStatuses.Cancelled, ct)
            ?? throw new NotFoundException("Cita no encontrada.", "slot_not_found");
        var time = ParseSlotTime(slotTime);
        var label = string.IsNullOrWhiteSpace(studentLabel) ? null : studentLabel.Trim();

        if (studentUserId is int sid && sid != booking.StudentUserId)
        {
            await _eligibility.EnsureEligibleAsync(schoolUserId, sid, ct);
            await EnsureNoOtherUpcomingBookingAsync(schoolUserId, sid, booking.Id, ct);
        }

        if (booking.ExamDate == date && booking.SlotTime == time)
        {
            booking.StudentUserId = studentUserId;
            booking.StudentLabel = label;
            booking.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            booking.UpdatedAt = _clock.UtcNow;
            await SaveBookingChangeAsync(ct);
            return booking;
        }

        var moved = await BookCoreAsync(new BookingRequest(
            schoolUserId, date, time, studentUserId, label, notes, actorUserId,
            ByStudent: false, GrowCapacityIfNeeded: false), ct);
        MarkCancelled(booking, actorUserId);
        SchoolAudit.Add(_db, _clock.UtcNow, schoolUserId, actorUserId, SchoolAuditAreas.ExamBooking,
            "booking_moved",
            $"Cita movida de {D(booking.ExamDate)} {T(booking.SlotTime)} a {D(date)} {T(time)}.",
            entityType: "exam_booking", entityId: moved.Id, studentUserId: studentUserId,
            oldValue: $"{D(booking.ExamDate)} {T(booking.SlotTime)}", newValue: $"{D(date)} {T(time)}");
        await _db.SaveChangesAsync(ct);
        return moved;
    }

    private void MarkCancelled(TheoryExamAppointment booking, int actorUserId)
    {
        var now = _clock.UtcNow;
        booking.Status = TheoryExamBookingStatuses.Cancelled;
        booking.CancelledAt = now;
        booking.CancelledByUserId = actorUserId;
        booking.UpdatedAt = now;
    }

    private async Task EnsureNoOtherUpcomingBookingAsync(
        int schoolUserId,
        int studentUserId,
        int? ignoreBookingId,
        CancellationToken ct)
    {
        var today = TodayColombia();
        var upcoming = await _db.Set<TheoryExamAppointment>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId
                && x.Status == TheoryExamBookingStatuses.Active
                && x.ExamDate >= today
                && (ignoreBookingId == null || x.Id != ignoreBookingId))
            .OrderBy(x => x.ExamDate)
            .ThenBy(x => x.SlotTime)
            .ToListAsync(ct);
        var next = upcoming.FirstOrDefault(x => !IsPast(x.ExamDate, x.SlotTime));
        if (next is not null)
        {
            throw new DomainException(
                $"Ya tiene un examen agendado el {Human(next.ExamDate, next.SlotTime)}. Cancélalo antes de agendar otro.",
                409,
                "exam_already_booked");
        }
    }

    /// <summary>
    /// Takes one seat of the hour inside a transaction. Two people can never get the same seat: the slot is
    /// locked, occupancy is re-counted, the first free seat number is used and a unique index backs it up.
    /// </summary>
    private async Task<TheoryExamAppointment> BookCoreAsync(BookingRequest request, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            TheoryExamAppointment? booking = null;
            TheoryExamScheduleOverride? grown = null;
            try
            {
                await LockAsync(request.SchoolUserId, request.Date, request.Time, request.StudentUserId, ct);
                var slot = await ResolveSlotAsync(request.SchoolUserId, request.Date, request.Time, ct);
                var taken = await LiveBookings(request.SchoolUserId)
                    .AsNoTracking()
                    .Where(x => x.ExamDate == request.Date && x.SlotTime == request.Time)
                    .Select(x => new { x.SeatNumber, x.StudentUserId })
                    .ToListAsync(ct);

                if (request.StudentUserId is int sid && taken.Any(x => x.StudentUserId == sid))
                {
                    throw new DomainException(
                        "El estudiante ya tiene cita en ese horario.",
                        409,
                        "exam_already_booked");
                }

                var capacity = slot?.Capacity ?? 0;
                if (request.GrowCapacityIfNeeded && (slot is null || slot.IsClosed || taken.Count >= capacity))
                {
                    capacity = Math.Max(capacity, taken.Count + 1);
                    grown = await GrowCapacityAsync(request, capacity, ct);
                }
                else if (slot is null)
                {
                    throw new DomainException(
                        "Ese horario no existe. Créalo primero en Horarios semanales o agrega un horario extra.",
                        404,
                        "exam_slot_not_found");
                }
                else if (slot.IsClosed)
                {
                    throw new DomainException("Ese horario está cerrado.", 409, "exam_slot_closed");
                }
                else if (taken.Count >= capacity)
                {
                    throw new DomainException(
                        "El horario ya no tiene cupos disponibles.",
                        409,
                        "exam_slot_full");
                }

                var usedSeats = taken.Select(x => x.SeatNumber).ToHashSet();
                var seat = Enumerable.Range(1, capacity).First(n => !usedSeats.Contains(n));
                var now = _clock.UtcNow;
                booking = new TheoryExamAppointment
                {
                    SchoolUserId = request.SchoolUserId,
                    ExamDate = request.Date,
                    SlotTime = request.Time,
                    StudentUserId = request.StudentUserId,
                    StudentLabel = request.StudentLabel,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    Status = TheoryExamBookingStatuses.Active,
                    SeatNumber = seat,
                    BookedByUserId = request.ActorUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Set<TheoryExamAppointment>().Add(booking);
                await _db.SaveChangesAsync(ct);

                SchoolAudit.Add(_db, now, request.SchoolUserId, request.ActorUserId, SchoolAuditAreas.ExamBooking,
                    "booking_created",
                    (request.ByStudent ? "El estudiante agendó" : "La escuela agendó")
                        + $" examen para el {D(request.Date)} {T(request.Time)} (cupo {seat} de {capacity}).",
                    entityType: "exam_booking", entityId: booking.Id, studentUserId: request.StudentUserId,
                    newValue: TheoryExamBookingStatuses.Active);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return booking;
            }
            catch (DbUpdateException) when (attempt < MaxBookingAttempts)
            {
                await tx.RollbackAsync(ct);
                Detach(booking, grown);
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync(ct);
                Detach(booking, grown);
                throw new DomainException(
                    "El horario ya no tiene cupos disponibles.",
                    409,
                    "exam_slot_full");
            }
            catch
            {
                await tx.RollbackAsync(ct);
                Detach(booking, grown);
                throw;
            }
        }
    }

    private async Task<TheoryExamScheduleOverride> GrowCapacityAsync(
        BookingRequest request,
        int capacity,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var ov = await _db.Set<TheoryExamScheduleOverride>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == request.SchoolUserId
                && x.Date == request.Date
                && x.StartTime == request.Time
                && !x.IsWholeDay, ct);
        if (ov is null)
        {
            ov = new TheoryExamScheduleOverride
            {
                SchoolUserId = request.SchoolUserId,
                Date = request.Date,
                StartTime = request.Time,
                CreatedByUserId = request.ActorUserId,
                CreatedAt = now
            };
            _db.Set<TheoryExamScheduleOverride>().Add(ov);
        }

        ov.IsClosed = false;
        ov.Capacity = capacity;
        ov.Note ??= "Creado por importación de Excel";
        ov.UpdatedAt = now;
        SchoolAudit.Add(_db, now, request.SchoolUserId, request.ActorUserId, SchoolAuditAreas.ExamSchedule,
            "slot_capacity_changed",
            $"Cupo del {D(request.Date)} {T(request.Time)} ajustado a {capacity} por importación de Excel.",
            entityType: "exam_override", newValue: capacity.ToString());
        return ov;
    }

    private void Detach(params object?[] entities)
    {
        foreach (var entity in entities)
        {
            if (entity is not null)
            {
                _db.Entry(entity).State = EntityState.Detached;
            }
        }

        foreach (var entry in _db.ChangeTracker.Entries<SchoolAuditEntry>()
            .Where(e => e.State == EntityState.Added)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task SaveBookingChangeAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            throw new DomainException("El estudiante ya tiene cita en ese horario.", 409, "exam_already_booked");
        }
    }
}
