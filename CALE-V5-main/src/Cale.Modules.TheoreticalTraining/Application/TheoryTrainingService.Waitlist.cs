using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryTrainingService
{
    private static readonly TimeSpan WaitlistOfferTtl = TimeSpan.FromHours(2);

    public async Task<ScheduleWaitlistEntryDto> JoinTheoryWaitlistAsync(
        int studentUserId,
        int sessionId,
        CancellationToken ct)
    {
        var student = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado.", "unauthorized");
        if (student.Role != Roles.Student || student.SchoolId is not int schoolUserId)
        {
            throw new ForbiddenException("Solo estudiantes vinculados a una escuela pueden entrar a la lista.", "not_student");
        }

        await EnsureStudentCanReserveAsync(studentUserId, schoolUserId, ct);
        var session = await RequireSessionAsync(schoolUserId, sessionId, ct);
        ValidateReservationWindow(session);
        var enrollment = await GetEnrollmentAsync(schoolUserId, studentUserId, ct);
        ValidateStudentSessionAccess(enrollment, session);

        var occupied = await CountOccupiedSeatsAsync(sessionId, ct);
        if (occupied < session.Capacity)
        {
            throw new DomainException(
                "Todavía hay cupo en esta clase. Resérvala directamente.",
                400,
                "class_has_seats");
        }

        var existingActive = await _db.Set<TheoryClassReservation>()
            .AnyAsync(x => x.ClassSessionId == sessionId
                && x.StudentUserId == studentUserId
                && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status), ct);
        if (existingActive)
        {
            throw new DomainException("Ya tienes esta clase reservada.", 400, "already_reserved");
        }

        var existingQueue = await _db.Set<ScheduleWaitlistEntry>()
            .FirstOrDefaultAsync(x => x.Kind == ScheduleWaitlistKinds.TheorySession
                && x.TargetId == sessionId
                && x.StudentUserId == studentUserId
                && ScheduleWaitlistStatuses.QueueStatuses.Contains(x.Status), ct);
        if (existingQueue is not null)
        {
            return MapWaitlist(existingQueue, session);
        }

        var maxPos = await _db.Set<ScheduleWaitlistEntry>()
            .Where(x => x.Kind == ScheduleWaitlistKinds.TheorySession
                && x.TargetId == sessionId
                && ScheduleWaitlistStatuses.QueueStatuses.Contains(x.Status))
            .Select(x => (int?)x.Position)
            .MaxAsync(ct) ?? 0;

        var now = _clock.UtcNow;
        var entry = new ScheduleWaitlistEntry
        {
            SchoolUserId = schoolUserId,
            Kind = ScheduleWaitlistKinds.TheorySession,
            TargetId = sessionId,
            StudentUserId = studentUserId,
            Position = maxPos + 1,
            Status = ScheduleWaitlistStatuses.Waiting,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Set<ScheduleWaitlistEntry>().AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
        return MapWaitlist(entry, session);
    }

    public async Task CancelTheoryWaitlistAsync(int studentUserId, int waitlistId, CancellationToken ct)
    {
        var entry = await _db.Set<ScheduleWaitlistEntry>()
            .FirstOrDefaultAsync(x => x.Id == waitlistId && x.StudentUserId == studentUserId, ct)
            ?? throw new NotFoundException("Entrada de lista de espera no encontrada.", "waitlist_not_found");

        if (!ScheduleWaitlistStatuses.QueueStatuses.Contains(entry.Status))
        {
            return;
        }

        entry.Status = ScheduleWaitlistStatuses.Cancelled;
        entry.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduleWaitlistEntryDto>> ListMyTheoryWaitlistAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var entries = await _db.Set<ScheduleWaitlistEntry>()
            .AsNoTracking()
            .Where(x => x.StudentUserId == studentUserId
                && ScheduleWaitlistStatuses.QueueStatuses.Contains(x.Status))
            .OrderBy(x => x.CreatedAt)
            .Take(30)
            .ToListAsync(ct);

        var theoryIds = entries
            .Where(x => x.Kind == ScheduleWaitlistKinds.TheorySession)
            .Select(x => x.TargetId)
            .Distinct()
            .ToList();
        var practicalIds = entries
            .Where(x => x.Kind == ScheduleWaitlistKinds.PracticalLesson)
            .Select(x => x.TargetId)
            .Distinct()
            .ToList();

        var theorySessions = await _db.Set<TheoryClassSession>()
            .AsNoTracking()
            .Where(x => theoryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var practicalSessions = await _db.Set<PracticalLessonSession>()
            .AsNoTracking()
            .Where(x => practicalIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        return entries.Select(e =>
        {
            if (e.Kind == ScheduleWaitlistKinds.TheorySession
                && theorySessions.TryGetValue(e.TargetId, out var ts))
            {
                return MapWaitlist(e, ts);
            }

            if (e.Kind == ScheduleWaitlistKinds.PracticalLesson
                && practicalSessions.TryGetValue(e.TargetId, out var ps))
            {
                return new ScheduleWaitlistEntryDto(
                    e.Id, e.Kind, e.TargetId, e.Position, e.Status,
                    e.OfferedAt?.ToString("O"), e.OfferExpiresAt?.ToString("O"),
                    ps.SessionDate.ToString("yyyy-MM-dd"), ps.StartTime.ToString("HH:mm"));
            }

            return new ScheduleWaitlistEntryDto(
                e.Id, e.Kind, e.TargetId, e.Position, e.Status,
                e.OfferedAt?.ToString("O"), e.OfferExpiresAt?.ToString("O"),
                e.TargetDate?.ToString("yyyy-MM-dd"),
                e.TargetTime?.ToString("HH:mm"));
        }).ToList();
    }

    /// <summary>
    /// Confirms an offered waitlist seat. Re-checks eligibility and capacity; never books if ineligible.
    /// </summary>
    public async Task<TheoryClassSessionDto> ConfirmTheoryWaitlistOfferAsync(
        int studentUserId,
        int waitlistId,
        CancellationToken ct)
    {
        var entry = await _db.Set<ScheduleWaitlistEntry>()
            .FirstOrDefaultAsync(x => x.Id == waitlistId
                && x.StudentUserId == studentUserId
                && x.Kind == ScheduleWaitlistKinds.TheorySession, ct)
            ?? throw new NotFoundException("Oferta no encontrada.", "waitlist_not_found");

        if (entry.Status != ScheduleWaitlistStatuses.Offered)
        {
            throw new DomainException("No hay una oferta activa para confirmar.", 400, "waitlist_not_offered");
        }

        if (entry.OfferExpiresAt is { } exp && _clock.UtcNow > exp)
        {
            entry.Status = ScheduleWaitlistStatuses.Expired;
            entry.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            var session = await RequireSessionAsync(entry.SchoolUserId, entry.TargetId, ct);
            await OfferNextTheoryWaitlistAsync(entry.SchoolUserId, entry.TargetId, session, ct);
            throw new DomainException(
                "La oferta expiró. Si aún hay cupo, el siguiente en la lista será notificado.",
                400,
                "waitlist_offer_expired");
        }

        try
        {
            var dto = await ReserveAsync(studentUserId, entry.TargetId, ct);
            entry.Status = ScheduleWaitlistStatuses.Confirmed;
            entry.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            return dto;
        }
        catch (DomainException)
        {
            entry.Status = ScheduleWaitlistStatuses.Expired;
            entry.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            var session = await RequireSessionAsync(entry.SchoolUserId, entry.TargetId, ct);
            await OfferNextTheoryWaitlistAsync(entry.SchoolUserId, entry.TargetId, session, ct);
            throw;
        }
    }

    internal async Task OfferNextTheoryWaitlistAsync(
        int schoolUserId,
        int sessionId,
        TheoryClassSession session,
        CancellationToken ct)
    {
        try
        {
            var now = _clock.UtcNow;
            var stale = await _db.Set<ScheduleWaitlistEntry>()
                .Where(x => x.Kind == ScheduleWaitlistKinds.TheorySession
                    && x.TargetId == sessionId
                    && x.Status == ScheduleWaitlistStatuses.Offered
                    && x.OfferExpiresAt != null
                    && x.OfferExpiresAt < now)
                .ToListAsync(ct);
            foreach (var s in stale)
            {
                s.Status = ScheduleWaitlistStatuses.Expired;
                s.UpdatedAt = now;
            }

            var occupied = await CountOccupiedSeatsAsync(sessionId, ct);
            if (occupied >= session.Capacity)
            {
                if (stale.Count > 0)
                {
                    await _db.SaveChangesAsync(ct);
                }

                return;
            }

            var hasOpenOffer = await _db.Set<ScheduleWaitlistEntry>()
                .AnyAsync(x => x.Kind == ScheduleWaitlistKinds.TheorySession
                    && x.TargetId == sessionId
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
                .Where(x => x.Kind == ScheduleWaitlistKinds.TheorySession
                    && x.TargetId == sessionId
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
            next.OfferExpiresAt = now.Add(WaitlistOfferTtl);
            next.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);

            await _notifications.NotifyUsersAsync(
                [next.StudentUserId],
                new NotificationDraft(
                    "Cupo liberado en teoría",
                    $"Hay un cupo para la clase del {session.SessionDate:dd/MM/yyyy} a las {session.StartTime:HH:mm}. Confirma en Mi formación en las próximas 2 horas.",
                    NotificationTypes.Reminder,
                    RelatedEntity: "theory_waitlist_offer",
                    RelatedId: next.Id,
                    Link: "/student/formacion",
                    Priority: NotificationPriorities.High,
                    DedupeKey: $"theory-waitlist-offer:{next.Id}"),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Theory waitlist offer failed for session {SessionId}", sessionId);
        }
    }

    private static ScheduleWaitlistEntryDto MapWaitlist(
        ScheduleWaitlistEntry entry,
        TheoryClassSession? session) =>
        new(
            entry.Id,
            entry.Kind,
            entry.TargetId,
            entry.Position,
            entry.Status,
            entry.OfferedAt?.ToString("O"),
            entry.OfferExpiresAt?.ToString("O"),
            entry.TargetDate?.ToString("yyyy-MM-dd") ?? session?.SessionDate.ToString("yyyy-MM-dd"),
            entry.TargetTime?.ToString("HH:mm") ?? session?.StartTime.ToString("HH:mm"));
}
