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

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed class TheoryTrainingService
{
    private readonly CaleDbContext _db;
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _schoolProfiles;
    private readonly ICatalogStore _catalog;
    private readonly IClock _clock;
    private readonly INotificationPublisher _notifications;

    public TheoryTrainingService(
        CaleDbContext db,
        IUserStore users,
        ISchoolProfileStore schoolProfiles,
        ICatalogStore catalog,
        IClock clock,
        INotificationPublisher notifications)
    {
        _db = db;
        _users = users;
        _schoolProfiles = schoolProfiles;
        _catalog = catalog;
        _clock = clock;
        _notifications = notifications;
    }

    // ── Topics ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TheoryTopicDto>> ListTopicsAsync(
        int schoolUserId,
        bool activeOnly,
        CancellationToken ct)
    {
        var query = _db.Set<TheoryTopic>().Where(x => x.SchoolUserId == schoolUserId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new TheoryTopicDto(x.Id, x.Name, x.Description, x.Color, x.Category, x.IsActive))
            .ToListAsync(ct);
    }

    public async Task<TheoryTopicDto> SaveTopicAsync(
        int schoolUserId,
        int? id,
        SaveTheoryTopicRequest request,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        TheoryTopic entity;
        if (id is > 0)
        {
            entity = await _db.Set<TheoryTopic>()
                .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == schoolUserId, ct)
                ?? throw new NotFoundException("Tema no encontrado.", "topic_not_found");
        }
        else
        {
            entity = new TheoryTopic { SchoolUserId = schoolUserId, CreatedAt = now };
            await _db.Set<TheoryTopic>().AddAsync(entity, ct);
        }

        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        entity.Color = string.IsNullOrWhiteSpace(request.Color) ? "#3B82F6" : request.Color.Trim();
        entity.Category = TheoryTopicCategories.IsValid(request.Category)
            ? request.Category
            : TheoryTopicCategories.InferFromName(request.Name);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return new TheoryTopicDto(entity.Id, entity.Name, entity.Description, entity.Color, entity.Category, entity.IsActive);
    }

    // ── Classrooms ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TheoryClassroomDto>> ListClassroomsAsync(
        int schoolUserId,
        bool activeOnly,
        CancellationToken ct)
    {
        var query = _db.Set<TheoryClassroom>().Where(x => x.SchoolUserId == schoolUserId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new TheoryClassroomDto(
                x.Id, x.Name, x.Identifier, x.Capacity, x.Location, x.IsActive))
            .ToListAsync(ct);
    }

    public async Task<TheoryClassroomDto> SaveClassroomAsync(
        int schoolUserId,
        int? id,
        SaveTheoryClassroomRequest request,
        CancellationToken ct)
    {
        if (request.Capacity < 1)
        {
            throw new DomainException("La capacidad debe ser al menos 1.", 400, "invalid_capacity");
        }

        var now = _clock.UtcNow;
        TheoryClassroom entity;
        if (id is > 0)
        {
            entity = await _db.Set<TheoryClassroom>()
                .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == schoolUserId, ct)
                ?? throw new NotFoundException("Aula no encontrada.", "classroom_not_found");
        }
        else
        {
            entity = new TheoryClassroom { SchoolUserId = schoolUserId, CreatedAt = now };
            await _db.Set<TheoryClassroom>().AddAsync(entity, ct);
        }

        entity.Name = request.Name.Trim();
        entity.Identifier = string.IsNullOrWhiteSpace(request.Identifier)
            ? null
            : request.Identifier.Trim();
        entity.Capacity = request.Capacity;
        entity.Location = string.IsNullOrWhiteSpace(request.Location)
            ? null
            : request.Location.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return new TheoryClassroomDto(
            entity.Id, entity.Name, entity.Identifier, entity.Capacity, entity.Location, entity.IsActive);
    }

    // ── Settings ────────────────────────────────────────────────────────

    public async Task<TheorySettingsDto> GetSettingsAsync(int schoolUserId, CancellationToken ct)
    {
        var settings = await EnsureBothSchedulingGroupsAsync(schoolUserId, ct);
        return MapSettings(settings);
    }

    public async Task<TheorySettingsDto> UpdateSettingsAsync(
        int schoolUserId,
        TheorySettingsDto request,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        settings.DefaultDurationMinutes = Math.Clamp(request.DefaultDurationMinutes, 30, 240);
        settings.MinCancelHours = Math.Clamp(request.MinCancelHours, 0, 72);
        settings.ReservationCloseMinutesBefore = Math.Clamp(request.ReservationCloseMinutesBefore, 0, 180);
        settings.RequiredTheoryHours = Math.Clamp(request.RequiredTheoryHours, 1, 200);
        settings.RequiredWorkshopHours = Math.Clamp(request.RequiredWorkshopHours, 0, 200);
        settings.TheoryExamId = request.TheoryExamId;
        settings.WeekdaysEnabled = true;
        settings.SaturdayEnabled = true;
        settings.NotifyReservationOpen = request.NotifyReservationOpen;
        settings.NotifyClassReminder24h = request.NotifyClassReminder24h;
        settings.NotifyClassReminder1h = request.NotifyClassReminder1h;
        settings.NotifyExamReminder24h = request.NotifyExamReminder24h;
        settings.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapSettings(settings);
    }

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
            settings.ReservationCloseMinutesBefore);

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
            settings.ReservationCloseMinutesBefore);

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
        await ValidateNoScheduleConflictAsync(studentUserId, session, enrollment, null, ct);

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

            return await MapSessionAsync(sessionId, studentUserId, ct);
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
    }

    // ── Attendance ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AttendanceRowDto>> ListAttendanceAsync(
        int schoolUserId,
        int sessionId,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(schoolUserId, sessionId, ct);
        var reservations = await _db.Set<TheoryClassReservation>()
            .Where(x => x.ClassSessionId == sessionId
                && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync(ct);
        var attendance = await _db.Set<TheoryAttendanceRecord>()
            .Where(x => x.ClassSessionId == sessionId)
            .ToDictionaryAsync(x => x.StudentUserId, ct);
        var rows = new List<AttendanceRowDto>();
        foreach (var r in reservations)
        {
            var user = await _users.GetByIdAsync(r.StudentUserId, ct);
            attendance.TryGetValue(r.StudentUserId, out var att);
            rows.Add(new AttendanceRowDto(
                r.StudentUserId,
                user?.Name ?? $"Estudiante {r.StudentUserId}",
                att?.Status ?? TheoryAttendanceStatuses.Pending,
                r.Id));
        }

        return rows.OrderBy(x => x.StudentName).ToList();
    }

    public async Task MarkAttendanceAsync(
        int schoolUserId,
        int sessionId,
        int markerUserId,
        MarkAttendanceRequest request,
        CancellationToken ct)
    {
        await RequireSessionAsync(schoolUserId, sessionId, ct);
        var now = _clock.UtcNow;
        var record = await _db.Set<TheoryAttendanceRecord>()
            .FirstOrDefaultAsync(x => x.ClassSessionId == sessionId
                && x.StudentUserId == request.StudentUserId, ct);
        if (record is null)
        {
            record = new TheoryAttendanceRecord
            {
                ClassSessionId = sessionId,
                StudentUserId = request.StudentUserId,
                CreatedAt = now
            };
            await _db.Set<TheoryAttendanceRecord>().AddAsync(record, ct);
        }

        record.Status = request.Status;
        record.MarkedByUserId = markerUserId;
        record.MarkedAt = now;
        record.Notes = request.Notes;
        record.UpdatedAt = now;

        var reservation = await _db.Set<TheoryClassReservation>()
            .FirstOrDefaultAsync(x => x.ClassSessionId == sessionId
                && x.StudentUserId == request.StudentUserId
                && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
        if (reservation is not null)
        {
            reservation.Status = request.Status switch
            {
                TheoryAttendanceStatuses.Present or TheoryAttendanceStatuses.Late => TheoryReservationStatuses.Attended,
                TheoryAttendanceStatuses.Absent => TheoryReservationStatuses.NoShow,
                _ => reservation.Status
            };
            reservation.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAttendanceBatchAsync(
        int schoolUserId,
        int sessionId,
        int markerUserId,
        MarkAttendanceBatchRequest request,
        CancellationToken ct)
    {
        foreach (var row in request.Rows)
        {
            await MarkAttendanceAsync(schoolUserId, sessionId, markerUserId, row, ct);
        }
    }

    public async Task<IReadOnlyList<TheoryClassSessionDto>> ListAttendanceSessionsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var fromDate = today.AddDays(-3);
        var toDate = today.AddDays(14);
        var sessionIds = await (
            from s in _db.Set<TheoryClassSession>()
            join r in _db.Set<TheoryClassReservation>() on s.Id equals r.ClassSessionId
            where s.SchoolUserId == schoolUserId
                && s.SessionDate >= fromDate
                && s.SessionDate <= toDate
                && s.Status != TheoryClassStatuses.Cancelled
                && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(r.Status)
            select s.Id)
            .Distinct()
            .ToListAsync(ct);

        var sessions = await _db.Set<TheoryClassSession>()
            .Where(s => sessionIds.Contains(s.Id))
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        var result = new List<TheoryClassSessionDto>();
        foreach (var s in sessions)
        {
            result.Add(await MapSessionAsync(s.Id, null, ct));
        }

        return result;
    }

    public async Task ProcessRemindersAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var recentOpen = now.AddMinutes(-10);

        var openedSessions = await _db.Set<TheoryClassSession>()
            .Include(x => x.Topic)
            .Where(x => x.Status == TheoryClassStatuses.Scheduled
                && x.ReservationOpenAt <= now
                && x.ReservationOpenAt > recentOpen)
            .ToListAsync(ct);

        foreach (var session in openedSessions)
        {
            var settings = await GetOrCreateSettingsAsync(session.SchoolUserId, ct);
            if (!settings.NotifyReservationOpen)
            {
                continue;
            }

            var students = await GetActiveStudentIdsAsync(session.SchoolUserId, ct);
            var reserved = await _db.Set<TheoryClassReservation>()
                .Where(x => x.ClassSessionId == session.Id
                    && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status))
                .Select(x => x.StudentUserId)
                .ToListAsync(ct);
            var targets = students.Except(reserved).ToList();
            foreach (var studentId in targets)
            {
                await _notifications.NotifyUsersAsync(
                    [studentId],
                    new NotificationDraft(
                        "Reservas abiertas",
                        $"Ya puedes reservar: {session.Topic?.Name ?? "Clase teórica"} · {session.SessionDate:dd/MM/yyyy} {session.StartTime:HH:mm}.",
                        NotificationTypes.TheoryClass,
                        RelatedEntity: "theory_class",
                        RelatedId: session.Id,
                        Link: "/student/training",
                        DedupeKey: $"theory:open:{session.Id}:{studentId}"),
                    ct);
            }
        }

        await SendClassRemindersAsync(
            now.AddHours(23),
            now.AddHours(25),
            settings => settings.NotifyClassReminder24h,
            "24h",
            "Recuerda que tienes clase mañana",
            ct);

        await SendClassRemindersAsync(
            now.AddMinutes(55),
            now.AddMinutes(65),
            settings => settings.NotifyClassReminder1h,
            "1h",
            "Tu clase comienza en 1 hora",
            ct);

        await SendExamRemindersAsync(now.AddHours(23), now.AddHours(25), ct);
    }

    private async Task SendExamRemindersAsync(
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct)
    {
        var fromDate = ColombiaTime.TodayInColombia().AddDays(-1);
        var toDate = fromDate.AddDays(3);
        var appointments = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.ExamDate >= fromDate
                && x.ExamDate <= toDate
                && x.StudentUserId != null)
            .ToListAsync(ct);

        foreach (var appointment in appointments)
        {
            var startUtc = ColombiaTime.ToUtc(appointment.ExamDate, appointment.SlotTime);
            if (startUtc < windowStart || startUtc > windowEnd)
            {
                continue;
            }

            var settings = await GetOrCreateSettingsAsync(appointment.SchoolUserId, ct);
            if (!settings.NotifyExamReminder24h)
            {
                continue;
            }

            await _notifications.NotifyUsersAsync(
                [appointment.StudentUserId!.Value],
                new NotificationDraft(
                    "Recordatorio: examen teórico mañana",
                    $"Tu examen teórico es el {appointment.ExamDate:dd/MM/yyyy} a las {appointment.SlotTime:HH:mm}.",
                    NotificationTypes.TheoryClass,
                    RelatedEntity: "theory_exam_appointment",
                    RelatedId: appointment.Id,
                    Link: "/student/training",
                    DedupeKey: $"theory:exam:24h:{appointment.Id}:{appointment.StudentUserId}"),
                ct);
        }
    }

    private async Task SendClassRemindersAsync(
        DateTime windowStart,
        DateTime windowEnd,
        Func<TheoryTrainingSettings, bool> enabled,
        string kind,
        string titlePrefix,
        CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var fromDate = today.AddDays(-1);
        var toDate = today.AddDays(2);
        var sessions = await _db.Set<TheoryClassSession>()
            .Include(x => x.Topic)
            .Include(x => x.Classroom)
            .Where(x => x.Status == TheoryClassStatuses.Scheduled
                && x.SessionDate >= fromDate
                && x.SessionDate <= toDate)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            var startUtc = ColombiaTime.ToUtc(session.SessionDate, session.StartTime);
            if (startUtc < windowStart || startUtc > windowEnd)
            {
                continue;
            }

            var settings = await GetOrCreateSettingsAsync(session.SchoolUserId, ct);
            if (!enabled(settings))
            {
                continue;
            }

            var studentIds = await _db.Set<TheoryClassReservation>()
                .Where(x => x.ClassSessionId == session.Id
                    && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status))
                .Select(x => x.StudentUserId)
                .ToListAsync(ct);

            foreach (var studentId in studentIds)
            {
                await _notifications.NotifyUsersAsync(
                    [studentId],
                    new NotificationDraft(
                        titlePrefix,
                        $"{session.Topic?.Name ?? "Clase teórica"} · {session.SessionDate:dd/MM/yyyy} {session.StartTime:HH:mm} · {session.Classroom?.Name ?? "Aula"}",
                        NotificationTypes.TheoryClass,
                        RelatedEntity: "theory_class",
                        RelatedId: session.Id,
                        Link: "/student/training",
                        DedupeKey: $"theory:remind:{kind}:{session.Id}:{studentId}"),
                    ct);
            }
        }
    }

    private async Task<List<int>> GetActiveStudentIdsAsync(int schoolUserId, CancellationToken ct)
    {
        var enrolled = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && StudentEnrollmentStatuses.CanReserveStatuses.Contains(x.Status))
            .Select(x => x.StudentUserId)
            .ToListAsync(ct);
        if (enrolled.Count > 0)
        {
            return enrolled;
        }

        var users = await _users.ListBySchoolAsync(schoolUserId, ct);
        return users
            .Where(x => x.Role == Roles.Student && x.IsActive)
            .Select(x => x.Id)
            .ToList();
    }

    // ── Dashboards ──────────────────────────────────────────────────────

    public async Task<TheorySchoolDashboardDto> GetSchoolDashboardAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var sessionsToday = await _db.Set<TheoryClassSession>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate == today
                && x.Status != TheoryClassStatuses.Cancelled)
            .ToListAsync(ct);
        var sessionIds = sessionsToday.Select(x => x.Id).ToList();
        var reserved = sessionIds.Count == 0
            ? 0
            : await _db.Set<TheoryClassReservation>()
                .CountAsync(x => sessionIds.Contains(x.ClassSessionId)
                    && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
        var capacity = sessionsToday.Sum(x => x.Capacity);
        var absences = sessionIds.Count == 0
            ? 0
            : await _db.Set<TheoryAttendanceRecord>()
                .CountAsync(x => sessionIds.Contains(x.ClassSessionId)
                    && x.Status == TheoryAttendanceStatuses.Absent, ct);
        var scheduled = await _db.Set<TheoryClassSession>()
            .CountAsync(x => x.SchoolUserId == schoolUserId
                && x.Status == TheoryClassStatuses.Scheduled
                && x.SessionDate >= today, ct);

        return new TheorySchoolDashboardDto(
            sessionsToday.Count,
            reserved,
            Math.Max(0, capacity - reserved),
            absences,
            scheduled);
    }

    public async Task<TheoryStudentDashboardDto> GetStudentDashboardAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var (schoolUserId, _) = await ResolveStudentSchoolAsync(studentUserId, ct);
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var today = ColombiaTime.TodayInColombia();
        var nowUtc = _clock.UtcNow;

        var myReservations = await _db.Set<TheoryClassReservation>()
            .Include(x => x.ClassSession)!.ThenInclude(s => s!.Topic)
            .Include(x => x.ClassSession)!.ThenInclude(s => s!.Classroom)
            .Where(x => x.StudentUserId == studentUserId
                && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var upcoming = myReservations
            .Where(r => r.ClassSession is not null)
            .Select(r => r.ClassSession!)
            .Where(s => ColombiaTime.ToUtc(s.SessionDate, s.StartTime) >= nowUtc
                && s.Status != TheoryClassStatuses.Cancelled)
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ToList();

        TheoryClassSessionDto? nextDto = null;
        if (upcoming.Count > 0)
        {
            nextDto = await MapSessionAsync(upcoming[0].Id, studentUserId, ct);
        }

        var upcomingDtos = new List<TheoryClassSessionDto>();
        foreach (var s in upcoming.Take(8))
        {
            upcomingDtos.Add(await MapSessionAsync(s.Id, studentUserId, ct));
        }

        var (theoryHours, workshopHours, absences) = await ComputeHoursBreakdownAsync(studentUserId, ct);
        var hoursRequired = settings.RequiredTheoryHours;
        var workshopRequired = settings.RequiredWorkshopHours;
        var progress = hoursRequired <= 0
            ? 0
            : Math.Round(theoryHours / hoursRequired * 100m, 1);

        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        var eligibility = await GetPracticalEligibilityAsync(
            schoolUserId,
            studentUserId,
            settings,
            theoryHours,
            workshopHours,
            enrollment?.TheoryExamAuthorized ?? false,
            enrollment?.PracticalAuthorized ?? false,
            ct);

        var (currentStreak, bestStreak) = await ComputeStreaksAsync(studentUserId, ct);
        var checkedIn = await _db.Set<StudentDailyCheckIn>()
            .AnyAsync(x => x.StudentUserId == studentUserId && x.CheckInDate == today, ct);

        var (nextAction, opensAt, countdownLabel) = await ComputeNextActionAsync(
            studentUserId,
            schoolUserId,
            enrollment?.AttendanceDayType,
            ct);

        var tasks = new List<TheoryDailyTaskDto>
        {
            new("Check-in diario", checkedIn),
            new("Revisar próxima clase", nextDto is not null),
            new("Reservar clase de mañana", false)
        };

        StudentExamAppointmentDto? nextExamAppointment = null;
        var examSlot = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId
                && x.ExamDate >= today)
            .OrderBy(x => x.ExamDate)
            .ThenBy(x => x.SlotTime)
            .FirstOrDefaultAsync(ct);
        if (examSlot is not null)
        {
            nextExamAppointment = new StudentExamAppointmentDto(
                examSlot.Id,
                examSlot.ExamDate.ToString("yyyy-MM-dd"),
                examSlot.SlotTime.ToString("HH:mm"));
        }

        StudentPlatformExamDto? platformExam = null;
        if (settings.TheoryExamId is int platformExamId)
        {
            var exams = await _catalog.ListPublishedExamsAsync(ct);
            var match = exams.FirstOrDefault(e => e.Id == platformExamId);
            if (match is not null)
            {
                platformExam = new StudentPlatformExamDto(match.Id, match.Name);
            }
        }

        return new TheoryStudentDashboardDto(
            nextDto,
            upcomingDtos,
            progress,
            theoryHours,
            hoursRequired,
            workshopHours,
            workshopRequired,
            Math.Max(0, upcoming.Count),
            absences,
            currentStreak,
            bestStreak,
            nextAction,
            countdownLabel,
            opensAt,
            checkedIn,
            tasks,
            enrollment?.AttendanceDayType,
            eligibility,
            nextExamAppointment,
            platformExam);
    }

    public async Task<TheoryWeekScheduleDto> GetStudentWeekScheduleAsync(
        int studentUserId,
        DateOnly? weekStart,
        CancellationToken ct)
    {
        var (schoolUserId, _) = await ResolveStudentSchoolAsync(studentUserId, ct);
        return await GetWeekScheduleAsync(schoolUserId, weekStart, studentUserId, ct);
    }

    public async Task CheckInAsync(int studentUserId, CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var exists = await _db.Set<StudentDailyCheckIn>()
            .AnyAsync(x => x.StudentUserId == studentUserId && x.CheckInDate == today, ct);
        if (exists)
        {
            return;
        }

        await _db.Set<StudentDailyCheckIn>().AddAsync(new StudentDailyCheckIn
        {
            StudentUserId = studentUserId,
            CheckInDate = today,
            CheckInAt = _clock.UtcNow
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TheoryExamOptionDto>> ListExamOptionsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var members = await _users.ListBySchoolAsync(schoolUserId, ct);
        var ownerIds = members
            .Where(m => m.Role is Roles.Teacher or Roles.School)
            .Select(m => m.Id)
            .ToHashSet();

        var exams = await _catalog.ListPublishedExamsAsync(ct);
        return exams
            .Where(e => ownerIds.Contains(e.CreatedById))
            .OrderBy(e => e.Name)
            .Select(e => new TheoryExamOptionDto(e.Id, e.Name))
            .ToList();
    }

    public async Task<(int SchoolUserId, User Student)> ResolveStudentSchoolPublicAsync(
        int studentUserId,
        CancellationToken ct) =>
        await ResolveStudentSchoolAsync(studentUserId, ct);

    public async Task<PracticalEligibilityDto> GetPracticalEligibilityAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var (theoryHours, workshopHours, _) = await ComputeHoursBreakdownAsync(studentUserId, ct);
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        return await GetPracticalEligibilityAsync(
            schoolUserId,
            studentUserId,
            settings,
            theoryHours,
            workshopHours,
            enrollment?.TheoryExamAuthorized ?? false,
            enrollment?.PracticalAuthorized ?? false,
            ct);
    }

    public async Task<(int ReadyForExamCount, int ReadyForPracticalCount, int NoExamAppointmentCount,
        IReadOnlyList<SchoolDashboardStudentRowDto> TopReadyForExam,
        IReadOnlyList<SchoolDashboardStudentRowDto> TopNoExamAppointment)> GetEnrollmentPipelineStatsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var students = (await _users.ListBySchoolAsync(schoolUserId, ct))
            .Where(x => x.Role == Roles.Student)
            .ToDictionary(x => x.Id);

        var enrollments = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && StudentEnrollmentStatuses.CanReserveStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var balances = await _db.Set<SchoolApprenticeProfile>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToDictionaryAsync(x => x.StudentUserId, x => x.BalanceDue, ct);

        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var studentsWithAppointment = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.ExamDate >= today
                && x.StudentUserId != null)
            .Select(x => x.StudentUserId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var appointmentSet = studentsWithAppointment.ToHashSet();

        var readyForExam = new List<SchoolDashboardStudentRowDto>();
        var readyForPractical = 0;
        var noExamAppointment = new List<SchoolDashboardStudentRowDto>();

        foreach (var enrollment in enrollments)
        {
            if (!students.TryGetValue(enrollment.StudentUserId, out var student))
            {
                continue;
            }

            balances.TryGetValue(enrollment.StudentUserId, out var balanceDue);
            var (theoryHours, workshopHours, _) = await ComputeHoursBreakdownAsync(
                enrollment.StudentUserId,
                ct);
            var eligibility = await GetPracticalEligibilityAsync(
                schoolUserId,
                enrollment.StudentUserId,
                settings,
                theoryHours,
                workshopHours,
                enrollment.TheoryExamAuthorized,
                enrollment.PracticalAuthorized,
                ct);

            if (balanceDue <= 0
                && !enrollment.TheoryExamAuthorized
                && eligibility.TheoryHoursComplete
                && eligibility.WorkshopHoursComplete
                && !eligibility.TheoryExamPassed)
            {
                readyForExam.Add(new SchoolDashboardStudentRowDto(
                    enrollment.StudentUserId,
                    student.Name));
            }

            if (balanceDue <= 0
                && !enrollment.PracticalAuthorized
                && eligibility.TheoryExamPassed)
            {
                readyForPractical++;
            }

            if (enrollment.TheoryExamAuthorized
                && !eligibility.TheoryExamPassed
                && !appointmentSet.Contains(enrollment.StudentUserId))
            {
                noExamAppointment.Add(new SchoolDashboardStudentRowDto(
                    enrollment.StudentUserId,
                    student.Name));
            }
        }

        return (
            readyForExam.Count,
            readyForPractical,
            noExamAppointment.Count,
            readyForExam.Take(5).ToList(),
            noExamAppointment.Take(5).ToList());
    }

    public async Task OnPlatformTheoryExamPassedAsync(
        int studentUserId,
        int examId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(studentUserId, ct);
        if (user?.SchoolId is not int schoolUserId)
        {
            return;
        }

        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        if (settings.TheoryExamId != examId)
        {
            return;
        }

        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        if (enrollment is null || !enrollment.TheoryExamAuthorized)
        {
            return;
        }

        var now = _clock.UtcNow;
        enrollment.TheoryExamAuthorized = false;
        enrollment.TheoryExamAuthorizedAt = null;
        enrollment.UpdatedAt = now;
        await LogAuthorizationEventAsync(
            schoolUserId,
            studentUserId,
            EnrollmentAuthorizationTypes.TheoryExam,
            EnrollmentAuthorizationActions.Revoked,
            null,
            ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EnrollmentAuthorizationEventDto>> ListAuthorizationHistoryAsync(
        int schoolUserId,
        int? studentUserId,
        int limit,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = _db.Set<EnrollmentAuthorizationEvent>()
            .Where(x => x.SchoolUserId == schoolUserId);
        if (studentUserId is int sid)
        {
            query = query.Where(x => x.StudentUserId == sid);
        }

        var events = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        var performerIds = events
            .Where(x => x.PerformedByUserId is > 0)
            .Select(x => x.PerformedByUserId!.Value)
            .Distinct()
            .ToList();
        var performerNames = new Dictionary<int, string>();
        foreach (var id in performerIds)
        {
            performerNames[id] = (await _users.GetByIdAsync(id, ct))?.Name ?? "Usuario";
        }

        return events
            .Select(e => new EnrollmentAuthorizationEventDto(
                e.AuthorizationType,
                e.Action,
                e.PerformedByUserId is int pid
                    ? performerNames.GetValueOrDefault(pid, "Usuario")
                    : "Sistema",
                e.CreatedAt))
            .ToList();
    }

    // ── Enrollments ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<EnrollmentDto>> ListEnrollmentsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var students = (await _users.ListBySchoolAsync(schoolUserId, ct))
            .Where(x => x.Role == Roles.Student)
            .OrderBy(x => x.Name)
            .ToList();
        var items = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToDictionaryAsync(x => x.StudentUserId, ct);
        var balances = await _db.Set<SchoolApprenticeProfile>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToDictionaryAsync(x => x.StudentUserId, x => x.BalanceDue, ct);

        var result = new List<EnrollmentDto>();
        foreach (var student in students)
        {
            var (theoryHours, workshopHours, _) = await ComputeHoursBreakdownAsync(student.Id, ct);
            items.TryGetValue(student.Id, out var enrollmentRow);
            var eligibility = await GetPracticalEligibilityAsync(
                schoolUserId,
                student.Id,
                settings,
                theoryHours,
                workshopHours,
                enrollmentRow?.TheoryExamAuthorized ?? false,
                enrollmentRow?.PracticalAuthorized ?? false,
                ct);
            balances.TryGetValue(student.Id, out var balanceDue);
            if (items.TryGetValue(student.Id, out var e))
            {
                result.Add(MapEnrollmentDto(e, student.Name, student.Email ?? "", eligibility, balanceDue));
            }
            else
            {
                result.Add(new EnrollmentDto(
                    0,
                    student.Id,
                    student.Name,
                    student.Email ?? "",
                    StudentEnrollmentStatuses.Pending,
                    null,
                    null,
                    null,
                    false,
                    false,
                    DateTime.UtcNow,
                    null,
                    eligibility,
                    balanceDue));
            }
        }

        return result;
    }

    public async Task<EnrollmentDto> UpdateEnrollmentAsync(
        int schoolUserId,
        int studentUserId,
        UpdateEnrollmentRequest request,
        int? actorUserId,
        CancellationToken ct)
    {
        var student = (await _users.ListBySchoolAsync(schoolUserId, ct))
            .FirstOrDefault(x => x.Id == studentUserId && x.Role == Roles.Student)
            ?? throw new NotFoundException("Estudiante no encontrado.", "student_not_found");

        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);

        var now = _clock.UtcNow;
        if (enrollment is null)
        {
            enrollment = new SchoolStudentEnrollment
            {
                SchoolUserId = schoolUserId,
                StudentUserId = studentUserId,
                CreatedAt = now
            };
            await _db.Set<SchoolStudentEnrollment>().AddAsync(enrollment, ct);
        }

        enrollment.Status = request.Status;
        enrollment.UpdatedAt = now;
        enrollment.AllowedStartTime = null;

        if (!string.IsNullOrWhiteSpace(request.AttendanceDayType))
        {
            var dayType = request.AttendanceDayType.Trim();
            if (!StudentAttendanceDayTypes.IsValid(dayType))
            {
                throw new DomainException("Tipo de día no válido.", 400, "invalid_day_type");
            }

            ActivateSchedulingGroup(settings, dayType);
            enrollment.AttendanceDayType = dayType;
        }

        if (request.LicenseCategories is not null)
        {
            var categories = request.LicenseCategories.Trim();
            if (categories.Length == 0)
            {
                enrollment.LicenseCategories = null;
            }
            else if (!StudentLicenseCategories.IsValid(categories))
            {
                throw new DomainException("Categoría de licencia no válida.", 400, "invalid_license_category");
            }
            else
            {
                enrollment.LicenseCategories = StudentLicenseCategories.Presets
                    .First(p => p.Equals(categories, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (request.Status is StudentEnrollmentStatuses.Accepted or StudentEnrollmentStatuses.Active)
        {
            enrollment.AcceptedAt ??= now;
            if (enrollment.AttendanceDayType is null)
            {
                throw new DomainException(
                    "Indica si el estudiante asiste en Semana o los sábados.",
                    400,
                    "day_type_required");
            }

            if (string.IsNullOrWhiteSpace(enrollment.LicenseCategories))
            {
                throw new DomainException(
                    "Indica la categoría de licencia que cursa el estudiante.",
                    400,
                    "license_category_required");
            }
        }

        if (request.Status == StudentEnrollmentStatuses.Suspended)
        {
            enrollment.SuspendedAt = now;
        }
        else if (StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status))
        {
            enrollment.SuspendedAt = null;
            enrollment.AcceptedAt ??= now;
        }
        else if (!string.IsNullOrWhiteSpace(enrollment.AttendanceDayType)
            && !string.IsNullOrWhiteSpace(enrollment.LicenseCategories)
            && enrollment.Status == StudentEnrollmentStatuses.Pending)
        {
            enrollment.Status = StudentEnrollmentStatuses.Active;
            enrollment.AcceptedAt ??= now;
            enrollment.SuspendedAt = null;
        }

        var (theoryHours, workshopHours, _) = await ComputeHoursBreakdownAsync(studentUserId, ct);
        var notifyTheoryExam = false;
        var notifyPractical = false;
        var prevTheoryAuth = enrollment.TheoryExamAuthorized;
        var prevPracticalAuth = enrollment.PracticalAuthorized;

        if (request.TheoryExamAuthorized is bool theoryExamAuthorized)
        {
            if (theoryExamAuthorized && !enrollment.TheoryExamAuthorized)
            {
                var hoursCheck = await GetPracticalEligibilityAsync(
                    schoolUserId,
                    studentUserId,
                    settings,
                    theoryHours,
                    workshopHours,
                    enrollment.TheoryExamAuthorized,
                    enrollment.PracticalAuthorized,
                    ct);
                if (!hoursCheck.TheoryHoursComplete || !hoursCheck.WorkshopHoursComplete)
                {
                    throw new DomainException(
                        "El estudiante debe completar las horas de teoría y taller antes de autorizar el examen.",
                        400,
                        "theory_hours_incomplete");
                }

                if (hoursCheck.TheoryExamPassed)
                {
                    throw new DomainException(
                        "El estudiante ya aprobó el examen teórico.",
                        400,
                        "theory_exam_already_passed");
                }

                await EnsureNoBalanceDueAsync(schoolUserId, studentUserId, ct);
                notifyTheoryExam = true;
            }

            enrollment.TheoryExamAuthorized = theoryExamAuthorized;
            enrollment.TheoryExamAuthorizedAt = theoryExamAuthorized ? now : null;
        }

        if (request.PracticalAuthorized is bool practicalAuthorized)
        {
            if (practicalAuthorized && !enrollment.PracticalAuthorized)
            {
                var examCheck = await GetPracticalEligibilityAsync(
                    schoolUserId,
                    studentUserId,
                    settings,
                    theoryHours,
                    workshopHours,
                    true,
                    false,
                    ct);
                if (!examCheck.TheoryExamPassed)
                {
                    throw new DomainException(
                        "El estudiante debe aprobar el examen teórico antes de autorizar manejo.",
                        400,
                        "theory_exam_required");
                }

                await EnsureNoBalanceDueAsync(schoolUserId, studentUserId, ct);
                notifyPractical = true;
            }

            enrollment.PracticalAuthorized = practicalAuthorized;
            enrollment.PracticalAuthorizedAt = practicalAuthorized ? now : null;
        }

        if (request.TheoryExamAuthorized is bool theoryFlag
            && theoryFlag != prevTheoryAuth)
        {
            await LogAuthorizationEventAsync(
                schoolUserId,
                studentUserId,
                EnrollmentAuthorizationTypes.TheoryExam,
                theoryFlag ? EnrollmentAuthorizationActions.Granted : EnrollmentAuthorizationActions.Revoked,
                actorUserId,
                ct);
        }

        if (request.PracticalAuthorized is bool practicalFlag
            && practicalFlag != prevPracticalAuth)
        {
            await LogAuthorizationEventAsync(
                schoolUserId,
                studentUserId,
                EnrollmentAuthorizationTypes.Practical,
                practicalFlag ? EnrollmentAuthorizationActions.Granted : EnrollmentAuthorizationActions.Revoked,
                actorUserId,
                ct);
        }

        await _db.SaveChangesAsync(ct);

        if (notifyTheoryExam)
        {
            await NotifyTheoryExamAuthorizedAsync(studentUserId, enrollment.Id, ct);
        }

        if (notifyPractical)
        {
            await NotifyPracticalAuthorizedAsync(studentUserId, enrollment.Id, ct);
        }
        var eligibility = await GetPracticalEligibilityAsync(
            schoolUserId,
            studentUserId,
            settings,
            theoryHours,
            workshopHours,
            enrollment.TheoryExamAuthorized,
            enrollment.PracticalAuthorized,
            ct);
        var balanceDue = await GetBalanceDueAsync(schoolUserId, studentUserId, ct);
        return MapEnrollmentDto(enrollment, student.Name, student.Email ?? "", eligibility, balanceDue);
    }

    public async Task<BulkAuthorizeEnrollmentsResultDto> BulkAuthorizeEnrollmentsAsync(
        int schoolUserId,
        BulkAuthorizeEnrollmentsRequest request,
        int? actorUserId,
        CancellationToken ct)
    {
        if (!request.TheoryExam && !request.Practical)
        {
            throw new DomainException("Indica qué autorización aplicar.", 400, "invalid_request");
        }

        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var enrollments = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        var authorized = 0;
        var skipped = 0;
        var skippedInactive = 0;
        var skippedBalanceDue = 0;
        var skippedAlreadyAuthorized = 0;
        var skippedHoursIncomplete = 0;
        var skippedExamPassed = 0;
        var skippedExamNotPassed = 0;
        var skippedAlreadyPractical = 0;
        var theoryExamNotified = new List<(int StudentUserId, int EnrollmentId)>();
        var practicalNotified = new List<(int StudentUserId, int EnrollmentId)>();

        foreach (var enrollment in enrollments)
        {
            if (!StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status))
            {
                skipped++;
                skippedInactive++;
                continue;
            }

            if (await GetBalanceDueAsync(schoolUserId, enrollment.StudentUserId, ct) > 0)
            {
                skipped++;
                skippedBalanceDue++;
                continue;
            }

            var (theoryHours, workshopHours, _) = await ComputeHoursBreakdownAsync(
                enrollment.StudentUserId,
                ct);
            var eligibility = await GetPracticalEligibilityAsync(
                schoolUserId,
                enrollment.StudentUserId,
                settings,
                theoryHours,
                workshopHours,
                enrollment.TheoryExamAuthorized,
                enrollment.PracticalAuthorized,
                ct);

            if (request.TheoryExam)
            {
                if (enrollment.TheoryExamAuthorized)
                {
                    skipped++;
                    skippedAlreadyAuthorized++;
                    continue;
                }

                if (!eligibility.TheoryHoursComplete || !eligibility.WorkshopHoursComplete)
                {
                    skipped++;
                    skippedHoursIncomplete++;
                    continue;
                }

                if (eligibility.TheoryExamPassed)
                {
                    skipped++;
                    skippedExamPassed++;
                    continue;
                }

                enrollment.TheoryExamAuthorized = true;
                enrollment.TheoryExamAuthorizedAt = now;
                enrollment.UpdatedAt = now;
                await LogAuthorizationEventAsync(
                    schoolUserId,
                    enrollment.StudentUserId,
                    EnrollmentAuthorizationTypes.TheoryExam,
                    EnrollmentAuthorizationActions.Granted,
                    actorUserId,
                    ct);
                theoryExamNotified.Add((enrollment.StudentUserId, enrollment.Id));
                authorized++;
                continue;
            }

            if (enrollment.PracticalAuthorized)
            {
                skipped++;
                skippedAlreadyPractical++;
                continue;
            }

            if (!eligibility.TheoryExamPassed)
            {
                skipped++;
                skippedExamNotPassed++;
                continue;
            }

            enrollment.PracticalAuthorized = true;
            enrollment.PracticalAuthorizedAt = now;
            enrollment.UpdatedAt = now;
            await LogAuthorizationEventAsync(
                schoolUserId,
                enrollment.StudentUserId,
                EnrollmentAuthorizationTypes.Practical,
                EnrollmentAuthorizationActions.Granted,
                actorUserId,
                ct);
            practicalNotified.Add((enrollment.StudentUserId, enrollment.Id));
            authorized++;
        }

        await _db.SaveChangesAsync(ct);

        foreach (var (studentUserId, enrollmentId) in theoryExamNotified)
        {
            await NotifyTheoryExamAuthorizedAsync(studentUserId, enrollmentId, ct);
        }

        foreach (var (studentUserId, enrollmentId) in practicalNotified)
        {
            await NotifyPracticalAuthorizedAsync(studentUserId, enrollmentId, ct);
        }

        return new BulkAuthorizeEnrollmentsResultDto(
            authorized,
            skipped,
            skippedInactive,
            skippedBalanceDue,
            skippedAlreadyAuthorized,
            skippedHoursIncomplete,
            skippedExamPassed,
            skippedExamNotPassed,
            skippedAlreadyPractical);
    }

    public async Task<EnrollmentDto> UpdateEnrollmentByIdAsync(
        int schoolUserId,
        int enrollmentId,
        UpdateEnrollmentRequest request,
        int? actorUserId,
        CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.Id == enrollmentId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Inscripción no encontrada.", "enrollment_not_found");

        return await UpdateEnrollmentAsync(schoolUserId, enrollment.StudentUserId, request, actorUserId, ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private async Task<TheoryClassSessionDto> MapSessionAsync(
        int sessionId,
        int? studentUserId,
        CancellationToken ct,
        StudentScheduleContext? studentCtx = null)
    {
        var session = await _db.Set<TheoryClassSession>()
            .Include(x => x.Topic)
            .Include(x => x.Classroom)
            .FirstAsync(x => x.Id == sessionId, ct);

        var occupied = await CountOccupiedSeatsAsync(sessionId, ct);
        TheoryClassReservation? mine = null;
        if (studentUserId is > 0)
        {
            mine = await _db.Set<TheoryClassReservation>()
                .FirstOrDefaultAsync(x => x.ClassSessionId == sessionId
                    && x.StudentUserId == studentUserId
                    && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
        }

        string? instructorName = null;
        if (session.InstructorUserId is int instructorId)
        {
            instructorName = (await _users.GetByIdAsync(instructorId, ct))?.Name;
        }

        var (state, message) = ComputeBookingState(
            session,
            occupied,
            mine,
            _clock.UtcNow,
            studentCtx);
        return new TheoryClassSessionDto(
            session.Id,
            session.SessionDate,
            session.StartTime.ToString("HH:mm"),
            session.EndTime.ToString("HH:mm"),
            session.TopicId,
            session.Topic?.Name ?? "",
            session.Topic?.Color ?? "#3B82F6",
            session.ClassroomId,
            session.Classroom?.Name ?? "",
            session.Capacity,
            occupied,
            Math.Max(0, session.Capacity - occupied),
            session.Status,
            session.InstructorUserId,
            instructorName,
            session.Notes,
            session.ReservationOpenAt,
            session.ReservationCloseAt,
            state,
            message,
            mine?.Id,
            mine?.Status);
    }

    private static (string State, string Message) ComputeBookingState(
        TheoryClassSession session,
        int occupied,
        TheoryClassReservation? mine,
        DateTime nowUtc,
        StudentScheduleContext? studentCtx = null)
    {
        if (session.Status == TheoryClassStatuses.Cancelled)
        {
            return ("cancelled", "Cancelada");
        }

        var startUtc = ColombiaTime.ToUtc(session.SessionDate, session.StartTime);
        if (nowUtc >= startUtc)
        {
            return ("started", "Clase iniciada");
        }

        if (mine is not null && TheoryReservationStatuses.ActiveStatuses.Contains(mine.Status))
        {
            return ("reserved", "Reservada");
        }

        if (occupied >= session.Capacity)
        {
            return ("full", "Sin cupos");
        }

        if (nowUtc < session.ReservationOpenAt)
        {
            var colombiaNow = ColombiaTime.NowInColombia();
            var openLocal = TimeZoneInfo.ConvertTimeFromUtc(
                session.ReservationOpenAt,
                ColombiaTime.TimeZone);
            if (openLocal.Date == colombiaNow.Date.AddDays(1))
            {
                return ("locked_tomorrow", "Disponible mañana");
            }

            return ("locked", "Reservas aún no abiertas");
        }

        if (nowUtc > session.ReservationCloseAt)
        {
            return ("closed", "Reservas cerradas");
        }

        if (studentCtx is not null)
        {
            var access = EvaluateStudentAccess(studentCtx.Enrollment, session, studentCtx);
            if (access is not null)
            {
                return access.Value;
            }
        }

        var available = session.Capacity - occupied;
        if (available == 1)
        {
            return ("can_reserve", "Último cupo disponible");
        }

        return ("can_reserve", $"{available} cupos disponibles");
    }

    private async Task<int> CountOccupiedSeatsAsync(int sessionId, CancellationToken ct) =>
        await _db.Set<TheoryClassReservation>()
            .CountAsync(x => x.ClassSessionId == sessionId
                && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);

    private async Task AcquireSessionLockAsync(int sessionId, CancellationToken ct)
    {
        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(@p0)",
                new object[] { sessionId },
                ct);
        }
    }

    private async Task ValidateNoScheduleConflictAsync(
        int studentUserId,
        TheoryClassSession target,
        SchoolStudentEnrollment enrollment,
        int? ignoreReservationId,
        CancellationToken ct)
    {
        var existing = await _db.Set<TheoryClassReservation>()
            .Include(x => x.ClassSession)
            .Where(x => x.StudentUserId == studentUserId
                && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status)
                && (ignoreReservationId == null || x.Id != ignoreReservationId))
            .ToListAsync(ct);

        var saturdayGroup = enrollment.AttendanceDayType == StudentAttendanceDayTypes.Saturday;
        var targetIsSaturday = target.SessionDate.DayOfWeek == DayOfWeek.Saturday;

        foreach (var r in existing)
        {
            var s = r.ClassSession;
            if (s is null)
            {
                continue;
            }

            if (s.SessionDate != target.SessionDate)
            {
                continue;
            }

            if (saturdayGroup && targetIsSaturday)
            {
                var countOnDay = existing.Count(x => x.ClassSession?.SessionDate == target.SessionDate);
                if (countOnDay >= TheoryAttendanceLimits.MaxSaturdayReservationsPerDay)
                {
                    throw new DomainException(
                        $"Ya reservaste {TheoryAttendanceLimits.MaxSaturdayReservationsPerDay} clases este sábado.",
                        400,
                        "saturday_day_limit");
                }

                continue;
            }

            throw new DomainException(
                "Ya tienes una clase reservada ese día. Solo puedes reservar una por día.",
                400,
                "day_already_reserved");
        }
    }

    private static void ValidateReservationWindow(TheoryClassSession session)
    {
        if (session.Status != TheoryClassStatuses.Scheduled)
        {
            throw new DomainException("Esta clase no acepta reservas.", 400, "class_not_bookable");
        }

        var now = ColombiaTime.UtcNow;
        if (now < session.ReservationOpenAt)
        {
            throw new DomainException(
                "Las reservas para esta clase aún no están abiertas.",
                400,
                "reservation_not_open");
        }

        if (now > session.ReservationCloseAt)
        {
            throw new DomainException("Las reservas para esta clase ya cerraron.", 400, "reservation_closed");
        }
    }

    private async Task EnsureStudentCanReserveAsync(
        int studentUserId,
        int schoolUserId,
        CancellationToken ct)
    {
        var student = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado.", "unauthorized");
        if (!student.IsActive)
        {
            throw new ForbiddenException("Tu cuenta no está activa.", "account_inactive");
        }

        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        var enrollment = await GetEnrollmentAsync(schoolUserId, studentUserId, ct);
        if (!CanStudentReserve(enrollment))
        {
            throw new ForbiddenException(
                "Tu escuela aún no te ha habilitado para reservar clases.",
                "enrollment_not_active");
        }
    }

    private static bool CanStudentReserve(SchoolStudentEnrollment enrollment) =>
        StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status)
        || (enrollment.Status == StudentEnrollmentStatuses.Pending
            && !string.IsNullOrWhiteSpace(enrollment.AttendanceDayType)
            && !string.IsNullOrWhiteSpace(enrollment.LicenseCategories));

    private async Task<(int SchoolUserId, User Student)> ResolveStudentSchoolAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var student = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado.", "unauthorized");
        if (student.SchoolId is not int schoolUserId)
        {
            throw new ForbiddenException(
                "Debes estar vinculado a una escuela.",
                "no_school");
        }

        return (schoolUserId, student);
    }

    private async Task<TheoryClassSession> RequireSessionAsync(
        int schoolUserId,
        int sessionId,
        CancellationToken ct) =>
        await _db.Set<TheoryClassSession>()
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Clase no encontrada.", "class_not_found");

    private async Task<TheoryTrainingSettings> GetOrCreateSettingsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var settings = await _db.Set<TheoryTrainingSettings>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId, ct);
        if (settings is not null)
        {
            return settings;
        }

        settings = new TheoryTrainingSettings
        {
            SchoolUserId = schoolUserId,
            UpdatedAt = _clock.UtcNow
        };
        await _db.Set<TheoryTrainingSettings>().AddAsync(settings, ct);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private async Task EnsureSchoolMembershipActiveAsync(int schoolUserId, CancellationToken ct)
    {
        var profile = await _schoolProfiles.GetByUserIdAsync(schoolUserId, ct);
        if (profile is null || !profile.IsCommerciallyActive(_clock.UtcNow))
        {
            throw new DomainException(
                "La membresía de la escuela no está activa.",
                400,
                "membership_inactive");
        }
    }

    private async Task<(decimal TheoryHours, decimal WorkshopHours, int Absences)> ComputeHoursBreakdownAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var records = await _db.Set<TheoryAttendanceRecord>()
            .Include(x => x.ClassSession)!.ThenInclude(s => s!.Topic)
            .Where(x => x.StudentUserId == studentUserId)
            .ToListAsync(ct);
        decimal theoryHours = 0;
        decimal workshopHours = 0;
        var absences = 0;
        foreach (var r in records)
        {
            if (r.Status is TheoryAttendanceStatuses.Present or TheoryAttendanceStatuses.Late)
            {
                var s = r.ClassSession;
                if (s is not null)
                {
                    var duration = (decimal)(s.EndTime - s.StartTime).TotalHours;
                    var category = s.Topic?.Category ?? TheoryTopicCategories.Theory;
                    if (category == TheoryTopicCategories.Workshop)
                    {
                        workshopHours += duration;
                    }
                    else
                    {
                        theoryHours += duration;
                    }
                }
            }
            else if (r.Status == TheoryAttendanceStatuses.Absent)
            {
                absences++;
            }
        }

        return (Math.Round(theoryHours, 1), Math.Round(workshopHours, 1), absences);
    }

    private async Task<PracticalEligibilityDto> GetPracticalEligibilityAsync(
        int schoolUserId,
        int studentUserId,
        TheoryTrainingSettings settings,
        decimal theoryHours,
        decimal workshopHours,
        bool theoryExamAuthorized,
        bool practicalAuthorized,
        CancellationToken ct)
    {
        var theoryExamPassed = true;
        if (settings.TheoryExamId is int examId)
        {
            theoryExamPassed = await _db.Set<Attempt>()
                .AnyAsync(a => a.UserId == studentUserId
                    && a.ExamId == examId
                    && a.FinishedAt != null
                    && a.Passed, ct);
        }

        var theoryComplete = theoryHours >= settings.RequiredTheoryHours;
        var workshopComplete = workshopHours >= settings.RequiredWorkshopHours;
        var canBook = theoryExamPassed && theoryComplete && workshopComplete && practicalAuthorized;

        string? blockReason = null;
        if (!theoryComplete)
        {
            blockReason = $"Te faltan horas de teoría ({theoryHours}/{settings.RequiredTheoryHours}).";
        }
        else if (!workshopComplete)
        {
            blockReason = $"Te faltan horas de taller ({workshopHours}/{settings.RequiredWorkshopHours}).";
        }
        else if (!theoryExamPassed)
        {
            blockReason = theoryExamAuthorized
                ? "Debes aprobar el examen teórico en la plataforma."
                : "Tu escuela debe autorizarte para presentar el examen teórico.";
        }
        else if (!practicalAuthorized)
        {
            blockReason = "Tu escuela debe autorizarte para clases de manejo.";
        }

        return new PracticalEligibilityDto(
            canBook,
            theoryExamPassed,
            theoryComplete,
            workshopComplete,
            theoryHours,
            settings.RequiredTheoryHours,
            workshopHours,
            settings.RequiredWorkshopHours,
            theoryExamAuthorized,
            practicalAuthorized,
            blockReason);
    }

    private async Task<(int Current, int Best)> ComputeStreaksAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var dates = await _db.Set<StudentDailyCheckIn>()
            .Where(x => x.StudentUserId == studentUserId)
            .OrderByDescending(x => x.CheckInDate)
            .Select(x => x.CheckInDate)
            .ToListAsync(ct);
        if (dates.Count == 0)
        {
            return (0, 0);
        }

        var today = ColombiaTime.TodayInColombia();
        var current = 0;
        if (dates[0] == today || dates[0] == today.AddDays(-1))
        {
            var cursor = dates[0];
            foreach (var d in dates)
            {
                if (d == cursor)
                {
                    current++;
                    cursor = cursor.AddDays(-1);
                }
                else if (d < cursor)
                {
                    break;
                }
            }
        }

        var best = 0;
        var run = 0;
        DateOnly? prev = null;
        foreach (var d in dates.OrderBy(x => x))
        {
            if (prev is null || d == prev.Value.AddDays(1))
            {
                run++;
            }
            else
            {
                run = 1;
            }

            best = Math.Max(best, run);
            prev = d;
        }

        return (current, best);
    }

    private async Task<(string? Action, DateTime? OpensAt, string? Countdown)> ComputeNextActionAsync(
        int studentUserId,
        int schoolUserId,
        string? attendanceDayType,
        CancellationToken ct)
    {
        var tomorrow = ColombiaTime.TodayInColombia().AddDays(1);
        var sessions = await _db.Set<TheoryClassSession>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate == tomorrow
                && x.Status == TheoryClassStatuses.Scheduled)
            .OrderBy(x => x.StartTime)
            .ToListAsync(ct);

        var session = sessions.FirstOrDefault(s =>
            attendanceDayType is null || SessionMatchesDayType(s.SessionDate, attendanceDayType));
        if (session is null)
        {
            return ("Consulta tu programación para planificar tu formación.", null, null);
        }

        var now = _clock.UtcNow;
        if (now < session.ReservationOpenAt)
        {
            var remaining = session.ReservationOpenAt - now;
            return (
                "Las reservas para mañana abrirán pronto.",
                session.ReservationOpenAt,
                $"{(int)remaining.TotalHours}h {remaining.Minutes}m");
        }

        var hasReservation = await _db.Set<TheoryClassReservation>()
            .AnyAsync(x => x.ClassSessionId == session.Id
                && x.StudentUserId == studentUserId
                && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status), ct);
        if (!hasReservation)
        {
            return ("Ya puedes reservar tu clase de mañana.", null, null);
        }

        return ("Recuerda asistir a tu clase reservada.", null, null);
    }

    private static TheorySettingsDto MapSettings(TheoryTrainingSettings s) =>
        new(
            s.DefaultDurationMinutes,
            s.MinCancelHours,
            s.ReservationCloseMinutesBefore,
            s.RequiredTheoryHours,
            s.RequiredWorkshopHours,
            s.TheoryExamId,
            s.WeekdaysEnabled,
            s.SaturdayEnabled,
            s.NotifyReservationOpen,
            s.NotifyClassReminder24h,
            s.NotifyClassReminder1h,
            s.NotifyExamReminder24h);

    private async Task LogAuthorizationEventAsync(
        int schoolUserId,
        int studentUserId,
        string authorizationType,
        string action,
        int? performedByUserId,
        CancellationToken ct)
    {
        await _db.Set<EnrollmentAuthorizationEvent>().AddAsync(new EnrollmentAuthorizationEvent
        {
            SchoolUserId = schoolUserId,
            StudentUserId = studentUserId,
            AuthorizationType = authorizationType,
            Action = action,
            PerformedByUserId = performedByUserId,
            CreatedAt = _clock.UtcNow
        }, ct);
    }

    private static EnrollmentDto MapEnrollmentDto(
        SchoolStudentEnrollment enrollment,
        string studentName,
        string studentEmail,
        PracticalEligibilityDto? eligibility = null,
        decimal balanceDue = 0) =>
        new(
            enrollment.Id,
            enrollment.StudentUserId,
            studentName,
            studentEmail,
            enrollment.Status,
            enrollment.AttendanceDayType,
            enrollment.AllowedStartTime?.ToString("HH:mm"),
            enrollment.LicenseCategories,
            enrollment.TheoryExamAuthorized,
            enrollment.PracticalAuthorized,
            enrollment.CreatedAt,
            enrollment.AcceptedAt,
            eligibility,
            balanceDue);

    private async Task<decimal> GetBalanceDueAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var profile = await _db.Set<SchoolApprenticeProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        return profile?.BalanceDue ?? 0;
    }

    private async Task EnsureNoBalanceDueAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var balance = await GetBalanceDueAsync(schoolUserId, studentUserId, ct);
        if (balance > 0)
        {
            throw new DomainException(
                "El estudiante tiene saldo pendiente. Registra el pago en Aprendices antes de autorizar.",
                400,
                "balance_due_pending");
        }
    }

    private Task NotifyTheoryExamAuthorizedAsync(
        int studentUserId,
        int enrollmentId,
        CancellationToken ct) =>
        _notifications.NotifyUsersAsync(
            [studentUserId],
            new NotificationDraft(
                "Autorizado para examen teórico",
                "Tu escuela te autorizó para presentar el examen teórico. Revisa Mi formación para ver los siguientes pasos.",
                NotificationTypes.TheoryClass,
                RelatedEntity: "theory_exam_auth",
                RelatedId: enrollmentId,
                Link: "/student/training"),
            ct);

    private Task NotifyPracticalAuthorizedAsync(
        int studentUserId,
        int enrollmentId,
        CancellationToken ct) =>
        _notifications.NotifyUsersAsync(
            [studentUserId],
            new NotificationDraft(
                "Autorizado para clases de manejo",
                "Tu escuela te autorizó para programar y reservar clases de manejo. Entra a Práctica para agendar.",
                NotificationTypes.TheoryClass,
                RelatedEntity: "practical_auth",
                RelatedId: enrollmentId,
                Link: "/student/practical"),
            ct);

    private static TimeOnly ParseTime(string value)
    {
        if (TimeOnly.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var time))
        {
            return time;
        }

        throw new DomainException("La hora no es válida.", 400, "invalid_time");
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private sealed record StudentScheduleContext(
        SchoolStudentEnrollment? Enrollment,
        IReadOnlyDictionary<DateOnly, int> ReservationsPerDate);

    private async Task<StudentScheduleContext> BuildStudentScheduleContextAsync(
        int studentUserId,
        int schoolUserId,
        DateOnly weekStart,
        DateOnly weekEnd,
        CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);

        var reservedDates = await _db.Set<TheoryClassReservation>()
            .Include(x => x.ClassSession)
            .Where(x => x.StudentUserId == studentUserId
                && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status)
                && x.ClassSession != null
                && x.ClassSession.SchoolUserId == schoolUserId
                && x.ClassSession.SessionDate >= weekStart
                && x.ClassSession.SessionDate <= weekEnd)
            .Select(x => x.ClassSession!.SessionDate)
            .ToListAsync(ct);

        var perDate = new Dictionary<DateOnly, int>();
        foreach (var date in reservedDates)
        {
            perDate[date] = perDate.GetValueOrDefault(date) + 1;
        }

        return new StudentScheduleContext(enrollment, perDate);
    }

    private async Task<SchoolStudentEnrollment> GetEnrollmentAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct) =>
        await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct)
        ?? throw new ForbiddenException(
            "Tu escuela aún no te ha habilitado para reservar clases.",
            "enrollment_not_active");

    private static void ValidateSessionDayForSettings(
        DateOnly sessionDate,
        TheoryTrainingSettings settings)
    {
        var isSaturday = sessionDate.DayOfWeek == DayOfWeek.Saturday;
        var isWeekday = sessionDate.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

        if (isSaturday)
        {
            if (!settings.SaturdayEnabled)
            {
                throw new DomainException("Las clases de sábado están desactivadas.", 400, "saturday_disabled");
            }

            return;
        }

        if (!isWeekday)
        {
            throw new DomainException(
                "La escuela solo programa clases de lunes a viernes o sábados.",
                400,
                "weekday_only");
        }

        if (!settings.WeekdaysEnabled)
        {
            throw new DomainException(
                "Las clases entre semana están desactivadas.",
                400,
                "weekday_disabled");
        }
    }

    private async Task<TheoryTrainingSettings> EnsureBothSchedulingGroupsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        if (settings.WeekdaysEnabled && settings.SaturdayEnabled)
        {
            return settings;
        }

        settings.WeekdaysEnabled = true;
        settings.SaturdayEnabled = true;
        settings.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private static void ActivateSchedulingGroup(TheoryTrainingSettings settings, string dayType)
    {
        if (dayType == StudentAttendanceDayTypes.Weekday)
        {
            settings.WeekdaysEnabled = true;
        }
        else if (dayType == StudentAttendanceDayTypes.Saturday)
        {
            settings.SaturdayEnabled = true;
        }
    }

    private static void ValidateStudentSessionAccess(
        SchoolStudentEnrollment enrollment,
        TheoryClassSession session)
    {
        var issue = EvaluateStudentAccessIssue(enrollment, session);
        if (issue is not null)
        {
            throw new DomainException(issue.Value.Message, 400, issue.Value.Code);
        }
    }

    private static (string State, string Message)? EvaluateStudentAccess(
        SchoolStudentEnrollment? enrollment,
        TheoryClassSession session,
        StudentScheduleContext ctx)
    {
        if (enrollment is null)
        {
            return ("locked", "No autorizado por la escuela");
        }

        if (enrollment.Status == StudentEnrollmentStatuses.Suspended)
        {
            return ("locked", "Tu acceso está suspendido. Pide a la escuela que te autorice de nuevo.");
        }

        if (!CanStudentReserve(enrollment))
        {
            return ("locked", "Pendiente de autorización por la escuela");
        }

        var dayLimit = EvaluateDayBookingLimit(enrollment, session, ctx);
        if (dayLimit is not null)
        {
            return dayLimit.Value;
        }

        var issue = EvaluateStudentAccessIssue(enrollment, session);
        if (issue is not null)
        {
            return ("locked", issue.Value.Message);
        }

        return null;
    }

    private static (string State, string Message)? EvaluateDayBookingLimit(
        SchoolStudentEnrollment enrollment,
        TheoryClassSession session,
        StudentScheduleContext ctx)
    {
        var count = ctx.ReservationsPerDate.GetValueOrDefault(session.SessionDate);
        if (count <= 0)
        {
            return null;
        }

        var isSaturdaySession = session.SessionDate.DayOfWeek == DayOfWeek.Saturday;
        var isSaturdayStudent = enrollment.AttendanceDayType == StudentAttendanceDayTypes.Saturday;

        if (isSaturdaySession && isSaturdayStudent)
        {
            if (count >= TheoryAttendanceLimits.MaxSaturdayReservationsPerDay)
            {
                return (
                    "day_limit",
                    $"Ya reservaste {TheoryAttendanceLimits.MaxSaturdayReservationsPerDay} clases este sábado");
            }

            return null;
        }

        return ("day_taken", "Ya reservaste una clase este día");
    }

    private static (string Code, string Message)? EvaluateStudentAccessIssue(
        SchoolStudentEnrollment enrollment,
        TheoryClassSession session)
    {
        if (enrollment.AttendanceDayType is null)
        {
            return ("day_not_assigned", "Día de asistencia no asignado");
        }

        var sessionDayType = session.SessionDate.DayOfWeek == DayOfWeek.Saturday
            ? StudentAttendanceDayTypes.Saturday
            : StudentAttendanceDayTypes.Weekday;

        if (!string.Equals(enrollment.AttendanceDayType, sessionDayType, StringComparison.OrdinalIgnoreCase))
        {
            var groupLabel = StudentAttendanceDayTypes.FormatLabel(enrollment.AttendanceDayType);
            return enrollment.AttendanceDayType == StudentAttendanceDayTypes.Weekday
                ? ("day_not_allowed", "Tu grupo es Semana; no puedes reservar los sábados")
                : ("day_not_allowed", $"Tu grupo es {groupLabel}; no puedes reservar entre semana");
        }

        return null;
    }

    private static bool SessionMatchesDayType(DateOnly sessionDate, string dayType)
    {
        var sessionDayType = sessionDate.DayOfWeek == DayOfWeek.Saturday
            ? StudentAttendanceDayTypes.Saturday
            : StudentAttendanceDayTypes.Weekday;

        return string.Equals(sessionDayType, dayType, StringComparison.OrdinalIgnoreCase);
    }
}
