using System.Globalization;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryExamScheduleService
{
    public async Task<StudentExamAvailabilityDto> GetStudentAvailabilityAsync(
        int studentUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var schoolUserId = await ResolveStudentSchoolAsync(studentUserId, ct);
        await _autoAuthorizer.TryAuthorizeAsync(schoolUserId, studentUserId, ct);
        var today = TodayColombia();
        var lastDay = today.AddDays(StudentMaxDaysAhead);
        var start = from is { } f && f > today ? f : today;
        var end = to ?? start.AddDays(13);
        if (end > lastDay)
        {
            end = lastDay;
        }

        (start, end) = ClampRange(start, end < start ? start : end);

        string? blockReason = null;
        try
        {
            await _eligibility.EnsureEligibleAsync(schoolUserId, studentUserId, ct);
        }
        catch (DomainException ex)
        {
            blockReason = ex.ErrorCode switch
            {
                "student_not_enrolled" => "Todavía no estás inscrito en tu escuela. Pídele a la escuela que te inscriba.",
                "student_not_authorized" => "Tu matrícula en la escuela no está activa. Comunícate con la escuela.",
                "theory_exam_not_authorized" => "Tu escuela todavía no te ha autorizado para el examen teórico.",
                "theory_hours_incomplete" => "Primero debes completar tus horas de teoría y taller.",
                "theory_exam_already_passed" => "¡Ya aprobaste el examen teórico!",
                "balance_due_pending" => "Tienes un saldo pendiente con la escuela. Ponte al día para agendar.",
                _ => ex.Message
            };
        }

        var settings = await _db.Set<TheoryTrainingSettings>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId, ct);
        var minCancelHours = settings?.MinCancelHours ?? 2;
        var myBooking = await FindUpcomingBookingAsync(schoolUserId, studentUserId, ct);

        var (templates, overrides) = await LoadScheduleAsync(schoolUserId, start, end, ct);
        var plan = ExamSlotPlanner.Plan(start, end, templates, overrides);
        var counts = await LiveBookings(schoolUserId)
            .Where(x => x.ExamDate >= start && x.ExamDate <= end)
            .GroupBy(x => new { x.ExamDate, x.SlotTime })
            .Select(g => new { g.Key.ExamDate, g.Key.SlotTime, Count = g.Count() })
            .ToListAsync(ct);
        var earliest = _clock.UtcNow.AddMinutes(StudentMinLeadMinutes);

        var days = new List<StudentExamDayDto>();
        foreach (var day in plan.Where(d => !d.IsClosed))
        {
            var slots = day.Slots
                .Where(s => !s.IsClosed && ColombiaTime.ToUtc(s.Date, s.Time) >= earliest)
                .Select(s =>
                {
                    var occupied = counts
                        .Where(c => c.ExamDate == s.Date && c.SlotTime == s.Time)
                        .Sum(c => c.Count);
                    var available = Math.Max(0, s.Capacity - occupied);
                    return new StudentExamSlotDto(
                        D(s.Date),
                        T(s.Time),
                        available,
                        available > 0 ? TheoryExamSlotStatuses.Available : TheoryExamSlotStatuses.Full);
                })
                .ToList();
            if (slots.Count > 0)
            {
                days.Add(new StudentExamDayDto(D(day.Date), slots));
            }
        }

        return new StudentExamAvailabilityDto(
            D(start),
            D(end),
            blockReason is null && myBooking is null,
            blockReason,
            myBooking is null ? null : MapStudentBooking(myBooking, minCancelHours),
            minCancelHours,
            days);
    }

    public async Task<StudentExamBookingDto> BookAsStudentAsync(
        int studentUserId,
        StudentBookExamRequest request,
        CancellationToken ct)
    {
        var schoolUserId = await ResolveStudentSchoolAsync(studentUserId, ct);
        var time = ParseSlotTime(request.Time);
        var start = ColombiaTime.ToUtc(request.Date, time);
        if (start <= _clock.UtcNow)
        {
            throw new DomainException("Ese horario ya pasó.", 400, "exam_slot_past");
        }

        if (start < _clock.UtcNow.AddMinutes(StudentMinLeadMinutes))
        {
            throw new DomainException(
                $"Debes agendar con al menos {StudentMinLeadMinutes / 60} horas de anticipación.",
                400,
                "exam_booking_too_soon");
        }

        if (request.Date > TodayColombia().AddDays(StudentMaxDaysAhead))
        {
            throw new DomainException(
                $"Solo puedes agendar hasta {StudentMaxDaysAhead} días adelante.",
                400,
                "exam_booking_too_far");
        }

        await _eligibility.EnsureEligibleAsync(schoolUserId, studentUserId, ct);
        await EnsureNoOtherUpcomingBookingAsync(schoolUserId, studentUserId, null, ct);

        var booking = await BookCoreAsync(new BookingRequest(
            schoolUserId,
            request.Date,
            time,
            studentUserId,
            null,
            null,
            studentUserId,
            ByStudent: true,
            GrowCapacityIfNeeded: false), ct);

        var studentName = (await LoadUserNamesAsync([studentUserId], ct)).GetValueOrDefault(studentUserId, "Un estudiante");
        await NotifySafeAsync(schoolUserId, new NotificationDraft(
            "Nuevo examen teórico agendado",
            $"{studentName} agendó su examen para el {Human(booking.ExamDate, booking.SlotTime)}.",
            NotificationTypes.TheoryClass,
            RelatedEntity: "theory_exam_appointment",
            RelatedId: booking.Id,
            Link: "/school/theory-exams"), ct);

        var minCancel = await MinCancelHoursAsync(schoolUserId, ct);
        return MapStudentBooking(booking, minCancel);
    }

    public async Task<IReadOnlyList<StudentExamBookingDto>> ListStudentBookingsAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var schoolUserId = await ResolveStudentSchoolAsync(studentUserId, ct);
        var minCancel = await MinCancelHoursAsync(schoolUserId, ct);
        var rows = await _db.Set<TheoryExamAppointment>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId)
            .OrderByDescending(x => x.ExamDate)
            .ThenByDescending(x => x.SlotTime)
            .Take(20)
            .ToListAsync(ct);
        return rows.Select(x => MapStudentBooking(x, minCancel)).ToList();
    }

    public async Task CancelAsStudentAsync(int studentUserId, int bookingId, CancellationToken ct)
    {
        var schoolUserId = await ResolveStudentSchoolAsync(studentUserId, ct);
        var booking = await _db.Set<TheoryExamAppointment>()
            .FirstOrDefaultAsync(x => x.Id == bookingId
                && x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct)
            ?? throw new NotFoundException("Cita no encontrada.", "booking_not_found");
        if (booking.Status == TheoryExamBookingStatuses.Cancelled)
        {
            return;
        }

        if (booking.Status != TheoryExamBookingStatuses.Active || IsPast(booking.ExamDate, booking.SlotTime))
        {
            throw new DomainException("Esta cita ya no se puede cancelar.", 400, "exam_cancel_too_late");
        }

        var minCancel = await MinCancelHoursAsync(schoolUserId, ct);
        if (_clock.UtcNow > ColombiaTime.ToUtc(booking.ExamDate, booking.SlotTime).AddHours(-minCancel))
        {
            throw new DomainException(
                $"Solo puedes cancelar hasta {minCancel} hora(s) antes del examen. Comunícate con tu escuela.",
                400,
                "exam_cancel_too_late");
        }

        MarkCancelled(booking, studentUserId);
        SchoolAudit.Add(_db, _clock.UtcNow, schoolUserId, studentUserId, SchoolAuditAreas.ExamBooking,
            "booking_cancelled",
            $"El estudiante canceló su examen del {D(booking.ExamDate)} {T(booking.SlotTime)}.",
            entityType: "exam_booking", entityId: booking.Id, studentUserId: studentUserId,
            oldValue: TheoryExamBookingStatuses.Active, newValue: TheoryExamBookingStatuses.Cancelled);
        await _db.SaveChangesAsync(ct);

        var studentName = (await LoadUserNamesAsync([studentUserId], ct)).GetValueOrDefault(studentUserId, "Un estudiante");
        await NotifySafeAsync(schoolUserId, new NotificationDraft(
            "Examen teórico cancelado",
            $"{studentName} canceló su examen del {Human(booking.ExamDate, booking.SlotTime)}. El cupo quedó libre.",
            NotificationTypes.TheoryClass,
            RelatedEntity: "theory_exam_appointment",
            RelatedId: booking.Id,
            Link: "/school/theory-exams"), ct);
    }

    private async Task<TheoryExamAppointment?> FindUpcomingBookingAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var today = TodayColombia();
        var rows = await _db.Set<TheoryExamAppointment>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId
                && x.Status == TheoryExamBookingStatuses.Active
                && x.ExamDate >= today)
            .OrderBy(x => x.ExamDate)
            .ThenBy(x => x.SlotTime)
            .ToListAsync(ct);
        return rows.FirstOrDefault(x => !IsPast(x.ExamDate, x.SlotTime));
    }

    private async Task<int> MinCancelHoursAsync(int schoolUserId, CancellationToken ct) =>
        await _db.Set<TheoryTrainingSettings>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId)
            .Select(x => (int?)x.MinCancelHours)
            .FirstOrDefaultAsync(ct) ?? 2;

    private StudentExamBookingDto MapStudentBooking(TheoryExamAppointment booking, int minCancelHours)
    {
        var startUtc = ColombiaTime.ToUtc(booking.ExamDate, booking.SlotTime);
        var deadlineUtc = startUtc.AddHours(-minCancelHours);
        var canCancel = booking.Status == TheoryExamBookingStatuses.Active && _clock.UtcNow <= deadlineUtc;
        var deadlineLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(deadlineUtc, DateTimeKind.Utc),
            ColombiaTime.TimeZone);
        return new StudentExamBookingDto(
            booking.Id,
            D(booking.ExamDate),
            T(booking.SlotTime),
            booking.Status,
            canCancel,
            booking.Status == TheoryExamBookingStatuses.Active
                ? deadlineLocal.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)
                : null);
    }
}
