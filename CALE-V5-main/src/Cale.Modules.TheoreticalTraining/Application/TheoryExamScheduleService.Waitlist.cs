using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryExamScheduleService
{
    private static readonly TimeSpan ExamWaitlistOfferTtl = TimeSpan.FromHours(2);

    public async Task<ScheduleWaitlistEntryDto> JoinExamWaitlistAsync(
        int studentUserId,
        StudentBookExamRequest request,
        CancellationToken ct)
    {
        var schoolUserId = await ResolveStudentSchoolAsync(studentUserId, ct);
        var time = ParseSlotTime(request.Time);
        if (IsPast(request.Date, time))
        {
            throw new DomainException("Ese horario ya pasó.", 400, "exam_slot_past");
        }

        await _eligibility.EnsureEligibleAsync(schoolUserId, studentUserId, ct);

        var slot = await ResolveSlotAsync(schoolUserId, request.Date, time, ct);
        if (slot is null || slot.IsClosed)
        {
            throw new DomainException("Ese horario no está disponible.", 400, "exam_slot_closed");
        }

        var taken = await LiveBookings(schoolUserId)
            .AsNoTracking()
            .CountAsync(x => x.ExamDate == request.Date && x.SlotTime == time, ct);
        if (taken < slot.Capacity)
        {
            throw new DomainException(
                "Todavía hay cupo en ese horario. Agenda directamente.",
                400,
                "exam_has_seats");
        }

        var existingQueue = await _db.Set<ScheduleWaitlistEntry>()
            .FirstOrDefaultAsync(x => x.Kind == ScheduleWaitlistKinds.TheoryExam
                && x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId
                && x.TargetDate == request.Date
                && x.TargetTime == time
                && ScheduleWaitlistStatuses.QueueStatuses.Contains(x.Status), ct);
        if (existingQueue is not null)
        {
            return MapExamWaitlist(existingQueue);
        }

        var maxPos = await _db.Set<ScheduleWaitlistEntry>()
            .Where(x => x.Kind == ScheduleWaitlistKinds.TheoryExam
                && x.SchoolUserId == schoolUserId
                && x.TargetDate == request.Date
                && x.TargetTime == time
                && ScheduleWaitlistStatuses.QueueStatuses.Contains(x.Status))
            .Select(x => (int?)x.Position)
            .MaxAsync(ct) ?? 0;

        var now = _clock.UtcNow;
        var entry = new ScheduleWaitlistEntry
        {
            SchoolUserId = schoolUserId,
            Kind = ScheduleWaitlistKinds.TheoryExam,
            TargetId = 0,
            TargetDate = request.Date,
            TargetTime = time,
            StudentUserId = studentUserId,
            Position = maxPos + 1,
            Status = ScheduleWaitlistStatuses.Waiting,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Set<ScheduleWaitlistEntry>().AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
        return MapExamWaitlist(entry);
    }

    public async Task<StudentExamBookingDto> ConfirmExamWaitlistOfferAsync(
        int studentUserId,
        int waitlistId,
        CancellationToken ct)
    {
        var entry = await _db.Set<ScheduleWaitlistEntry>()
            .FirstOrDefaultAsync(x => x.Id == waitlistId
                && x.StudentUserId == studentUserId
                && x.Kind == ScheduleWaitlistKinds.TheoryExam, ct)
            ?? throw new NotFoundException("Oferta no encontrada.", "waitlist_not_found");

        if (entry.Status != ScheduleWaitlistStatuses.Offered
            || entry.TargetDate is null
            || entry.TargetTime is null)
        {
            throw new DomainException("No hay una oferta activa para confirmar.", 400, "waitlist_not_offered");
        }

        if (entry.OfferExpiresAt is { } exp && _clock.UtcNow > exp)
        {
            entry.Status = ScheduleWaitlistStatuses.Expired;
            entry.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            await OfferNextExamWaitlistAsync(entry.SchoolUserId, entry.TargetDate.Value, entry.TargetTime.Value, ct);
            throw new DomainException(
                "La oferta expiró. Si aún hay cupo, el siguiente en la lista será notificado.",
                400,
                "waitlist_offer_expired");
        }

        try
        {
            var booking = await BookAsStudentAsync(
                studentUserId,
                new StudentBookExamRequest(entry.TargetDate.Value, T(entry.TargetTime.Value)),
                ct);
            entry.Status = ScheduleWaitlistStatuses.Confirmed;
            entry.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            return booking;
        }
        catch (DomainException)
        {
            entry.Status = ScheduleWaitlistStatuses.Expired;
            entry.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            await OfferNextExamWaitlistAsync(entry.SchoolUserId, entry.TargetDate.Value, entry.TargetTime.Value, ct);
            throw;
        }
    }

    internal async Task OfferNextExamWaitlistAsync(
        int schoolUserId,
        DateOnly date,
        TimeOnly time,
        CancellationToken ct)
    {
        try
        {
            var now = _clock.UtcNow;
            var stale = await _db.Set<ScheduleWaitlistEntry>()
                .Where(x => x.Kind == ScheduleWaitlistKinds.TheoryExam
                    && x.SchoolUserId == schoolUserId
                    && x.TargetDate == date
                    && x.TargetTime == time
                    && x.Status == ScheduleWaitlistStatuses.Offered
                    && x.OfferExpiresAt != null
                    && x.OfferExpiresAt < now)
                .ToListAsync(ct);
            foreach (var s in stale)
            {
                s.Status = ScheduleWaitlistStatuses.Expired;
                s.UpdatedAt = now;
            }

            var slot = await ResolveSlotAsync(schoolUserId, date, time, ct);
            if (slot is null || slot.IsClosed)
            {
                if (stale.Count > 0)
                {
                    await _db.SaveChangesAsync(ct);
                }

                return;
            }

            var taken = await LiveBookings(schoolUserId)
                .CountAsync(x => x.ExamDate == date && x.SlotTime == time, ct);
            if (taken >= slot.Capacity)
            {
                if (stale.Count > 0)
                {
                    await _db.SaveChangesAsync(ct);
                }

                return;
            }

            var hasOpenOffer = await _db.Set<ScheduleWaitlistEntry>()
                .AnyAsync(x => x.Kind == ScheduleWaitlistKinds.TheoryExam
                    && x.SchoolUserId == schoolUserId
                    && x.TargetDate == date
                    && x.TargetTime == time
                    && x.Status == ScheduleWaitlistStatuses.Offered
                    && (x.OfferExpiresAt == null || x.OfferExpiresAt >= now), ct);
            if (hasOpenOffer)
            {
                if (stale.Count > 0)
                {
                    await _db.SaveChangesAsync(ct);
                }

                return;
            }

            var next = await _db.Set<ScheduleWaitlistEntry>()
                .Where(x => x.Kind == ScheduleWaitlistKinds.TheoryExam
                    && x.SchoolUserId == schoolUserId
                    && x.TargetDate == date
                    && x.TargetTime == time
                    && x.Status == ScheduleWaitlistStatuses.Waiting)
                .OrderBy(x => x.Position)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (next is null)
            {
                if (stale.Count > 0)
                {
                    await _db.SaveChangesAsync(ct);
                }

                return;
            }

            next.Status = ScheduleWaitlistStatuses.Offered;
            next.OfferedAt = now;
            next.OfferExpiresAt = now.Add(ExamWaitlistOfferTtl);
            next.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);

            await NotifySafeAsync(next.StudentUserId, new NotificationDraft(
                "Cupo liberado en examen teórico",
                $"Hay un cupo el {Human(date, time)}. Confirma en Mi formación en las próximas 2 horas.",
                NotificationTypes.Reminder,
                RelatedEntity: "exam_waitlist_offer",
                RelatedId: next.Id,
                Link: "/student/formacion",
                Priority: NotificationPriorities.High,
                DedupeKey: $"exam-waitlist-offer:{next.Id}"), ct);
        }
        catch (Exception ex)
        {
            // Best-effort; cancellation/reschedule already committed.
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static ScheduleWaitlistEntryDto MapExamWaitlist(ScheduleWaitlistEntry entry) =>
        new(
            entry.Id,
            entry.Kind,
            entry.TargetId,
            entry.Position,
            entry.Status,
            entry.OfferedAt?.ToString("O"),
            entry.OfferExpiresAt?.ToString("O"),
            entry.TargetDate?.ToString("yyyy-MM-dd"),
            entry.TargetTime?.ToString("HH:mm"));
}
