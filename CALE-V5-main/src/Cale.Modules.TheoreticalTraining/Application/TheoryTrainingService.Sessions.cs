using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryTrainingService
{
    // ── Sessions ────────────────────────────────────────────────────────

    public async Task<TheoryClassSessionDto> CreateSessionAsync(
        int schoolUserId,
        CreateTheoryClassRequest request,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var topic = await _db.Set<TheoryTopic>()
            .FirstOrDefaultAsync(x => x.Id == request.TopicId && x.SchoolUserId == schoolUserId && x.IsActive, ct)
            ?? throw new DomainException("Tema no válido.", 400, "invalid_topic");
        var classroom = await _db.Set<TheoryClassroom>()
            .FirstOrDefaultAsync(x => x.Id == request.ClassroomId && x.SchoolUserId == schoolUserId && x.IsActive, ct)
            ?? throw new DomainException("Aula no válida.", 400, "invalid_classroom");

        if (request.SessionDate.DayOfWeek == DayOfWeek.Sunday)
        {
            throw new DomainException("No se programan clases los domingos.", 400, "sunday_disabled");
        }

        ValidateSessionDayForSettings(request.SessionDate, settings);

        var start = ParseTime(request.StartTime);
        var end = ParseTime(request.EndTime);
        if (end <= start)
        {
            throw new DomainException("La hora de fin debe ser posterior al inicio.", 400, "invalid_time");
        }

        var capacity = request.Capacity is > 0
            ? Math.Min(request.Capacity.Value, classroom.Capacity)
            : classroom.Capacity;

        var (openUtc, closeUtc) = ColombiaTime.ComputeReservationWindow(
            request.SessionDate,
            start,
            settings.ReservationCloseMinutesBefore,
            TheoryBookingPolicy.ReservationOpenDaysBefore(settings, request.SessionDate));

        var now = _clock.UtcNow;
        var session = new TheoryClassSession
        {
            SchoolUserId = schoolUserId,
            TopicId = topic.Id,
            ClassroomId = classroom.Id,
            InstructorUserId = request.InstructorUserId,
            SessionDate = request.SessionDate,
            StartTime = start,
            EndTime = end,
            Capacity = capacity,
            Status = TheoryClassStatuses.Scheduled,
            ReservationOpenAt = openUtc,
            ReservationCloseAt = closeUtc,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Set<TheoryClassSession>().AddAsync(session, ct);
        await _db.SaveChangesAsync(ct);
        return await MapSessionAsync(session.Id, null, ct);
    }

    public async Task<BulkCreateTheoryClassesResult> BulkCreateSessionsAsync(
        int schoolUserId,
        BulkCreateTheoryClassesRequest request,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var topic = await _db.Set<TheoryTopic>()
            .FirstOrDefaultAsync(x => x.Id == request.TopicId && x.SchoolUserId == schoolUserId && x.IsActive, ct)
            ?? throw new DomainException("Tema no válido.", 400, "invalid_topic");
        var classroom = await _db.Set<TheoryClassroom>()
            .FirstOrDefaultAsync(x => x.Id == request.ClassroomId && x.SchoolUserId == schoolUserId && x.IsActive, ct)
            ?? throw new DomainException("Aula no válida.", 400, "invalid_classroom");

        if (request.ToDate < request.FromDate)
        {
            throw new DomainException("La fecha final debe ser posterior o igual a la inicial.", 400, "invalid_date_range");
        }

        if (request.ToDate.DayNumber - request.FromDate.DayNumber > 366)
        {
            throw new DomainException("El rango máximo es de un año.", 400, "range_too_long");
        }

        var days = (request.DaysOfWeek ?? Array.Empty<int>())
            .Where(d => d is >= 0 and <= 6)
            .Distinct()
            .ToHashSet();
        if (days.Count == 0)
        {
            throw new DomainException("Selecciona al menos un día de la semana.", 400, "days_required");
        }

        var start = ParseTime(request.StartTime);
        var end = ParseTime(request.EndTime);
        if (end <= start)
        {
            throw new DomainException("La hora de fin debe ser posterior al inicio.", 400, "invalid_time");
        }

        var capacity = request.Capacity is > 0
            ? Math.Min(request.Capacity.Value, classroom.Capacity)
            : classroom.Capacity;
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var today = ColombiaTime.TodayInColombia();
        var from = request.FromDate < today ? today : request.FromDate;

        var existing = await _db.Set<TheoryClassSession>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate >= from
                && x.SessionDate <= request.ToDate
                && x.ClassroomId == classroom.Id
                && x.StartTime == start
                && x.Status != TheoryClassStatuses.Cancelled)
            .Select(x => x.SessionDate)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        var created = 0;
        var skippedExisting = 0;
        var skippedInvalid = 0;
        var now = _clock.UtcNow;
        var toAdd = new List<TheoryClassSession>();

        for (var date = from; date <= request.ToDate; date = date.AddDays(1))
        {
            if (!days.Contains((int)date.DayOfWeek))
            {
                continue;
            }

            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                skippedInvalid++;
                continue;
            }

            try
            {
                ValidateSessionDayForSettings(date, settings);
            }
            catch (DomainException)
            {
                skippedInvalid++;
                continue;
            }

            if (existingSet.Contains(date))
            {
                skippedExisting++;
                continue;
            }

            var (openUtc, closeUtc) = ColombiaTime.ComputeReservationWindow(
                date,
                start,
                settings.ReservationCloseMinutesBefore,
                TheoryBookingPolicy.ReservationOpenDaysBefore(settings, date));

            toAdd.Add(new TheoryClassSession
            {
                SchoolUserId = schoolUserId,
                TopicId = topic.Id,
                ClassroomId = classroom.Id,
                InstructorUserId = request.InstructorUserId,
                SessionDate = date,
                StartTime = start,
                EndTime = end,
                Capacity = capacity,
                Status = TheoryClassStatuses.Scheduled,
                ReservationOpenAt = openUtc,
                ReservationCloseAt = closeUtc,
                Notes = notes,
                CreatedAt = now,
                UpdatedAt = now
            });
            created++;
        }

        if (toAdd.Count > 0)
        {
            await _db.Set<TheoryClassSession>().AddRangeAsync(toAdd, ct);
            await _db.SaveChangesAsync(ct);
        }

        return new BulkCreateTheoryClassesResult(created, skippedExisting, skippedInvalid);
    }

    public async Task<BulkCancelTheoryClassesResult> BulkCancelSessionsAsync(
        int schoolUserId,
        BulkCancelTheoryClassesRequest request,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);

        if (request.ToDate < request.FromDate)
        {
            throw new DomainException("La fecha final debe ser posterior o igual a la inicial.", 400, "invalid_date_range");
        }

        if (request.ToDate.DayNumber - request.FromDate.DayNumber > 366)
        {
            throw new DomainException("El rango máximo es de un año.", 400, "range_too_long");
        }

        var days = (request.DaysOfWeek ?? Array.Empty<int>())
            .Where(d => d is >= 0 and <= 6)
            .Distinct()
            .ToHashSet();
        if (days.Count == 0)
        {
            throw new DomainException("Selecciona al menos un día de la semana.", 400, "days_required");
        }

        var start = ParseTime(request.StartTime);
        var end = ParseTime(request.EndTime);
        if (end <= start)
        {
            throw new DomainException("La hora de fin debe ser posterior al inicio.", 400, "invalid_time");
        }

        _ = await _db.Set<TheoryTopic>()
            .FirstOrDefaultAsync(x => x.Id == request.TopicId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new DomainException("Tema no válido.", 400, "invalid_topic");
        _ = await _db.Set<TheoryClassroom>()
            .FirstOrDefaultAsync(x => x.Id == request.ClassroomId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new DomainException("Aula no válida.", 400, "invalid_classroom");

        var candidates = await _db.Set<TheoryClassSession>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate >= request.FromDate
                && x.SessionDate <= request.ToDate
                && x.StartTime == start
                && x.EndTime == end
                && x.TopicId == request.TopicId
                && x.ClassroomId == request.ClassroomId
                && x.Status != TheoryClassStatuses.Cancelled)
            .ToListAsync(ct);

        var skippedInvalid = 0;
        var sessions = new List<TheoryClassSession>();
        foreach (var session in candidates)
        {
            if (!days.Contains((int)session.SessionDate.DayOfWeek)
                || session.SessionDate.DayOfWeek == DayOfWeek.Sunday)
            {
                skippedInvalid++;
                continue;
            }

            sessions.Add(session);
        }

        if (sessions.Count == 0)
        {
            return new BulkCancelTheoryClassesResult(0, 0, skippedInvalid);
        }

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var reservations = await _db.Set<TheoryClassReservation>()
            .Where(x => sessionIds.Contains(x.ClassSessionId))
            .ToListAsync(ct);
        var attendance = await _db.Set<TheoryAttendanceRecord>()
            .Where(x => sessionIds.Contains(x.ClassSessionId))
            .ToListAsync(ct);

        var withReservations = reservations
            .Where(r => TheoryReservationStatuses.OccupiesSeatStatuses.Contains(r.Status))
            .Select(r => r.ClassSessionId)
            .Distinct()
            .Count();

        var notifyByStudent = reservations
            .Where(r => TheoryReservationStatuses.OccupiesSeatStatuses.Contains(r.Status))
            .GroupBy(r => r.StudentUserId)
            .ToDictionary(g => g.Key, g => g.Count());

        if (attendance.Count > 0)
        {
            _db.Set<TheoryAttendanceRecord>().RemoveRange(attendance);
        }

        if (reservations.Count > 0)
        {
            _db.Set<TheoryClassReservation>().RemoveRange(reservations);
        }

        _db.Set<TheoryClassSession>().RemoveRange(sessions);
        await _db.SaveChangesAsync(ct);

        if (notifyByStudent.Count > 0)
        {
            var timeLabel = $"{start:HH\\:mm}";
            foreach (var (studentId, count) in notifyByStudent)
            {
                var body = count == 1
                    ? $"La escuela eliminó 1 clase teórica ({timeLabel}) entre {request.FromDate:dd/MM/yyyy} y {request.ToDate:dd/MM/yyyy}."
                    : $"La escuela eliminó {count} clases teóricas ({timeLabel}) entre {request.FromDate:dd/MM/yyyy} y {request.ToDate:dd/MM/yyyy}.";
                await _notifications.NotifyUsersAsync(
                    [studentId],
                    "Clases teóricas eliminadas",
                    body,
                    NotificationTypes.TheoryClass,
                    null,
                    "theory_class_bulk",
                    null,
                    ct);
            }
        }

        return new BulkCancelTheoryClassesResult(sessions.Count, withReservations, skippedInvalid);
    }

    public async Task<TheoryClassSessionDto> UpdateSessionAsync(
        int schoolUserId,
        int sessionId,
        UpdateTheoryClassRequest request,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var session = await RequireSessionAsync(schoolUserId, sessionId, ct);

        if (session.Status == TheoryClassStatuses.Cancelled)
        {
            throw new DomainException("Esta clase está cancelada.", 400, "class_cancelled");
        }

        var startUtc = ColombiaTime.ToUtc(session.SessionDate, session.StartTime);
        if (_clock.UtcNow >= startUtc)
        {
            throw new DomainException(
                "No puedes editar una clase que ya comenzó.",
                400,
                "class_already_started");
        }

        var topic = await _db.Set<TheoryTopic>()
            .FirstOrDefaultAsync(x => x.Id == request.TopicId && x.SchoolUserId == schoolUserId && x.IsActive, ct)
            ?? throw new DomainException("Tema no válido.", 400, "invalid_topic");
        var classroom = await _db.Set<TheoryClassroom>()
            .FirstOrDefaultAsync(x => x.Id == request.ClassroomId && x.SchoolUserId == schoolUserId && x.IsActive, ct)
            ?? throw new DomainException("Aula no válida.", 400, "invalid_classroom");

        if (request.SessionDate.DayOfWeek == DayOfWeek.Sunday)
        {
            throw new DomainException("No se programan clases los domingos.", 400, "sunday_disabled");
        }

        ValidateSessionDayForSettings(request.SessionDate, settings);

        var start = ParseTime(request.StartTime);
        var end = ParseTime(request.EndTime);
        if (end <= start)
        {
            throw new DomainException("La hora de fin debe ser posterior al inicio.", 400, "invalid_time");
        }

        var occupied = await CountOccupiedSeatsAsync(sessionId, ct);
        var capacity = request.Capacity is > 0
            ? Math.Min(request.Capacity.Value, classroom.Capacity)
            : classroom.Capacity;
        if (capacity < occupied)
        {
            throw new DomainException(
                $"El cupo no puede ser menor que las reservas actuales ({occupied}).",
                400,
                "capacity_below_reserved");
        }

        var (openUtc, closeUtc) = ColombiaTime.ComputeReservationWindow(
            request.SessionDate,
            start,
            settings.ReservationCloseMinutesBefore,
            TheoryBookingPolicy.ReservationOpenDaysBefore(settings, request.SessionDate));

        session.TopicId = topic.Id;
        session.ClassroomId = classroom.Id;
        session.InstructorUserId = request.InstructorUserId;
        session.SessionDate = request.SessionDate;
        session.StartTime = start;
        session.EndTime = end;
        session.Capacity = capacity;
        session.ReservationOpenAt = openUtc;
        session.ReservationCloseAt = closeUtc;
        session.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        session.UpdatedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await MapSessionAsync(session.Id, null, ct);
    }

    public async Task<TheoryMonthScheduleDto> GetMonthScheduleAsync(
        int schoolUserId,
        DateOnly? month,
        CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var anchor = month ?? new DateOnly(today.Year, today.Month, 1);
        var monthStart = new DateOnly(anchor.Year, anchor.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var sessions = await _db.Set<TheoryClassSession>()
            .Include(x => x.Topic)
            .Include(x => x.Classroom)
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate >= monthStart
                && x.SessionDate <= monthEnd
                && x.Status != TheoryClassStatuses.Cancelled)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(ct);

        var dtos = new List<TheoryClassSessionDto>();
        foreach (var s in sessions)
        {
            dtos.Add(await MapSessionAsync(s.Id, null, ct));
        }

        return new TheoryMonthScheduleDto(monthStart, monthEnd, dtos);
    }

    public async Task<TheoryWeekScheduleDto> GetWeekScheduleAsync(
        int schoolUserId,
        DateOnly? weekStart,
        int? studentUserId,
        CancellationToken ct)
    {
        var start = StartOfWeek(weekStart ?? ColombiaTime.TodayInColombia());
        var end = start.AddDays(6);
        var sessions = await _db.Set<TheoryClassSession>()
            .Include(x => x.Topic)
            .Include(x => x.Classroom)
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate >= start
                && x.SessionDate <= end
                && x.Status != TheoryClassStatuses.Cancelled)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(ct);

        var dtos = new List<TheoryClassSessionDto>();
        StudentScheduleContext? studentCtx = null;
        if (studentUserId is > 0)
        {
            studentCtx = await BuildStudentScheduleContextAsync(
                studentUserId.Value,
                schoolUserId,
                start,
                end,
                ct);
        }

        foreach (var s in sessions)
        {
            if (studentCtx?.Enrollment?.AttendanceDayType is not null
                && !SessionMatchesDayType(s.SessionDate, studentCtx.Enrollment.AttendanceDayType))
            {
                continue;
            }

            dtos.Add(await MapSessionAsync(s.Id, studentUserId, ct, studentCtx));
        }

        return new TheoryWeekScheduleDto(
            start,
            end,
            dtos,
            ColombiaTime.StandardTwoHourSlots
                .Select(s => new TheoryTimeSlotDto(
                    $"{s.Start:HH\\:mm} – {s.End:HH\\:mm}",
                    s.Start.ToString("HH:mm"),
                    s.End.ToString("HH:mm")))
                .ToList(),
            studentCtx?.Enrollment?.AttendanceDayType);
    }

    public async Task CancelSessionAsync(
        int schoolUserId,
        int sessionId,
        int actorUserId,
        string? reason,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        var session = await RequireSessionAsync(schoolUserId, sessionId, ct);

        var reservations = await _db.Set<TheoryClassReservation>()
            .Where(x => x.ClassSessionId == sessionId)
            .ToListAsync(ct);

        var studentIds = reservations
            .Where(r => TheoryReservationStatuses.OccupiesSeatStatuses.Contains(r.Status))
            .Select(r => r.StudentUserId)
            .Distinct()
            .ToList();

        var attendance = await _db.Set<TheoryAttendanceRecord>()
            .Where(x => x.ClassSessionId == sessionId)
            .ToListAsync(ct);

        if (attendance.Count > 0)
        {
            _db.Set<TheoryAttendanceRecord>().RemoveRange(attendance);
        }

        if (reservations.Count > 0)
        {
            _db.Set<TheoryClassReservation>().RemoveRange(reservations);
        }

        _db.Set<TheoryClassSession>().Remove(session);
        await _db.SaveChangesAsync(ct);

        if (studentIds.Count > 0)
        {
            await _notifications.NotifyUsersAsync(
                studentIds,
                "Clase teórica eliminada",
                $"La escuela eliminó la clase del {session.SessionDate:dd/MM/yyyy} a las {session.StartTime:HH:mm}.",
                NotificationTypes.TheoryClass,
                null,
                "theory_class",
                sessionId,
                ct);
        }
    }

    // ── Reservations ────────────────────────────────────────────────────

    public async Task<TheoryClassSessionDto> ReserveAsync(
        int studentUserId,
        int sessionId,
        CancellationToken ct)
    {
        var student = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado.", "unauthorized");
        if (student.Role != Roles.Student || student.SchoolId is not int schoolUserId)
        {
            throw new ForbiddenException("Solo estudiantes vinculados a una escuela pueden reservar.", "not_student");
        }

        await EnsureStudentCanReserveAsync(studentUserId, schoolUserId, ct);
        var session = await RequireSessionAsync(schoolUserId, sessionId, ct);
        ValidateReservationWindow(session);
        var enrollment = await GetEnrollmentAsync(schoolUserId, studentUserId, ct);
        ValidateStudentSessionAccess(enrollment, session);
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var policy = TheoryBookingPolicy.From(settings);
        await ValidateNoScheduleConflictAsync(studentUserId, session, enrollment, null, policy, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await AcquireSessionLockAsync(sessionId, ct);
            var occupied = await CountOccupiedSeatsAsync(sessionId, ct);
            if (occupied >= session.Capacity)
            {
                throw new DomainException(
                    "El último cupo acaba de ser reservado.",
                    409,
                    "class_full");
            }

            var existing = await _db.Set<TheoryClassReservation>()
                .FirstOrDefaultAsync(x => x.ClassSessionId == sessionId
                    && x.StudentUserId == studentUserId
                    && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status), ct);
            if (existing is not null)
            {
                throw new DomainException("Ya tienes esta clase reservada.", 400, "already_reserved");
            }

            var now = _clock.UtcNow;
            var reservation = new TheoryClassReservation
            {
                ClassSessionId = sessionId,
                StudentUserId = studentUserId,
                Status = TheoryReservationStatuses.Reserved,
                ReservedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _db.Set<TheoryClassReservation>().AddAsync(reservation, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _notifications.NotifyUserAsync(
                studentUserId,
                "Cupo reservado",
                $"Reservaste la clase del {session.SessionDate:dd/MM/yyyy} a las {session.StartTime:HH:mm}.",
                NotificationTypes.TheoryClass,
                null,
                "theory_reservation",
                reservation.Id,
                ct);

            var weekStart = StartOfWeek(session.SessionDate);
            var studentCtx = await BuildStudentScheduleContextAsync(
                studentUserId,
                schoolUserId,
                weekStart,
                weekStart.AddDays(6),
                ct);
            return await MapSessionAsync(sessionId, studentUserId, ct, studentCtx);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task CancelReservationAsync(
        int studentUserId,
        int reservationId,
        CancellationToken ct)
    {
        var reservation = await _db.Set<TheoryClassReservation>()
            .Include(x => x.ClassSession)
            .FirstOrDefaultAsync(x => x.Id == reservationId && x.StudentUserId == studentUserId, ct)
            ?? throw new NotFoundException("Reserva no encontrada.", "reservation_not_found");

        if (!TheoryReservationStatuses.ActiveStatuses.Contains(reservation.Status))
        {
            throw new DomainException("Esta reserva ya no está activa.", 400, "reservation_inactive");
        }

        var session = reservation.ClassSession
            ?? throw new DomainException("Clase no encontrada.", 404, "session_not_found");
        var settings = await GetOrCreateSettingsAsync(session.SchoolUserId, ct);
        var startUtc = ColombiaTime.ToUtc(session.SessionDate, session.StartTime);
        var minCancel = TimeSpan.FromHours(settings.MinCancelHours);
        if (_clock.UtcNow > startUtc - minCancel)
        {
            throw new DomainException(
                $"No puedes cancelar esta clase con menos de {settings.MinCancelHours} hora(s) de anticipación.",
                400,
                "cancel_too_late");
        }

        reservation.Status = TheoryReservationStatuses.CancelledByStudent;
        reservation.CancelledAt = _clock.UtcNow;
        reservation.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        await OfferNextTheoryWaitlistAsync(session.SchoolUserId, session.Id, session, ct);
    }

    /// <summary>
    /// Moves an active theory reservation to another session in one transaction.
    /// New seat is secured before the old one is marked <see cref="TheoryReservationStatuses.Rescheduled"/>.
    /// </summary>
    public async Task<TheoryClassSessionDto> RescheduleReservationAsync(
        int studentUserId,
        int reservationId,
        int newSessionId,
        CancellationToken ct)
    {
        var oldReservation = await _db.Set<TheoryClassReservation>()
            .Include(x => x.ClassSession)
            .FirstOrDefaultAsync(x => x.Id == reservationId && x.StudentUserId == studentUserId, ct)
            ?? throw new NotFoundException("Reserva no encontrada.", "reservation_not_found");

        if (!TheoryReservationStatuses.ActiveStatuses.Contains(oldReservation.Status))
        {
            throw new DomainException("Esta reserva ya no está activa.", 400, "reservation_inactive");
        }

        var oldSession = oldReservation.ClassSession
            ?? throw new DomainException("Clase no encontrada.", 404, "session_not_found");
        if (oldSession.Id == newSessionId)
        {
            throw new DomainException("Elige un horario distinto al actual.", 400, "same_session");
        }

        var settings = await GetOrCreateSettingsAsync(oldSession.SchoolUserId, ct);
        var startUtc = ColombiaTime.ToUtc(oldSession.SessionDate, oldSession.StartTime);
        var minCancel = TimeSpan.FromHours(settings.MinCancelHours);
        if (_clock.UtcNow > startUtc - minCancel)
        {
            throw new DomainException(
                $"No puedes reprogramar esta clase con menos de {settings.MinCancelHours} hora(s) de anticipación.",
                400,
                "cancel_too_late");
        }

        var schoolUserId = oldSession.SchoolUserId;
        await EnsureStudentCanReserveAsync(studentUserId, schoolUserId, ct);
        var newSession = await RequireSessionAsync(schoolUserId, newSessionId, ct);
        ValidateReservationWindow(newSession);
        var enrollment = await GetEnrollmentAsync(schoolUserId, studentUserId, ct);
        ValidateStudentSessionAccess(enrollment, newSession);
        var policy = TheoryBookingPolicy.From(settings);
        await ValidateNoScheduleConflictAsync(
            studentUserId, newSession, enrollment, oldReservation.Id, policy, ct);

        // Lock lower id first to avoid deadlocks when two reschedules cross.
        var firstLock = Math.Min(oldSession.Id, newSessionId);
        var secondLock = Math.Max(oldSession.Id, newSessionId);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await AcquireSessionLockAsync(firstLock, ct);
            if (secondLock != firstLock)
            {
                await AcquireSessionLockAsync(secondLock, ct);
            }

            var occupied = await CountOccupiedSeatsAsync(newSessionId, ct);
            if (occupied >= newSession.Capacity)
            {
                throw new DomainException(
                    "El nuevo horario ya no tiene cupo. Tu reserva original se mantiene.",
                    409,
                    "class_full");
            }

            var alreadyOnNew = await _db.Set<TheoryClassReservation>()
                .AnyAsync(x => x.ClassSessionId == newSessionId
                    && x.StudentUserId == studentUserId
                    && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status), ct);
            if (alreadyOnNew)
            {
                throw new DomainException("Ya tienes ese horario reservado.", 400, "already_reserved");
            }

            var now = _clock.UtcNow;
            var created = new TheoryClassReservation
            {
                ClassSessionId = newSessionId,
                StudentUserId = studentUserId,
                Status = TheoryReservationStatuses.Reserved,
                ReservedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _db.Set<TheoryClassReservation>().AddAsync(created, ct);

            oldReservation.Status = TheoryReservationStatuses.Rescheduled;
            oldReservation.CancelledAt = now;
            oldReservation.CancellationReason = $"Rescheduled to session {newSessionId}";
            oldReservation.UpdatedAt = now;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _notifications.NotifyUserAsync(
                studentUserId,
                "Clase reprogramada",
                $"Moviste tu clase al {newSession.SessionDate:dd/MM/yyyy} a las {newSession.StartTime:HH:mm}.",
                NotificationTypes.TheoryClass,
                null,
                "theory_reschedule",
                created.Id,
                ct);

            await OfferNextTheoryWaitlistAsync(schoolUserId, oldSession.Id, oldSession, ct);

            var weekStart = StartOfWeek(newSession.SessionDate);
            var studentCtx = await BuildStudentScheduleContextAsync(
                studentUserId,
                schoolUserId,
                weekStart,
                weekStart.AddDays(6),
                ct);
            return await MapSessionAsync(newSessionId, studentUserId, ct, studentCtx);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
