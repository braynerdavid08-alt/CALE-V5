using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
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
    private readonly IClock _clock;
    private readonly INotificationPublisher _notifications;

    public TheoryTrainingService(
        CaleDbContext db,
        IUserStore users,
        ISchoolProfileStore schoolProfiles,
        IClock clock,
        INotificationPublisher notifications)
    {
        _db = db;
        _users = users;
        _schoolProfiles = schoolProfiles;
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
            .Select(x => new TheoryTopicDto(x.Id, x.Name, x.Description, x.Color, x.IsActive))
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
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return new TheoryTopicDto(entity.Id, entity.Name, entity.Description, entity.Color, entity.IsActive);
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
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
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
        settings.SaturdayEnabled = request.SaturdayEnabled;
        settings.NotifyReservationOpen = request.NotifyReservationOpen;
        settings.NotifyClassReminder24h = request.NotifyClassReminder24h;
        settings.NotifyClassReminder1h = request.NotifyClassReminder1h;
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

        if (request.SessionDate.DayOfWeek == DayOfWeek.Saturday && !settings.SaturdayEnabled)
        {
            throw new DomainException("Las clases de sábado están desactivadas.", 400, "saturday_disabled");
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

    public async Task<TheoryWeekScheduleDto> GetWeekScheduleAsync(
        int schoolUserId,
        DateOnly? weekStart,
        int? studentUserId,
        CancellationToken ct)
    {
        var start = weekStart ?? StartOfWeek(ColombiaTime.TodayInColombia());
        var end = start.AddDays(6);
        var sessions = await _db.Set<TheoryClassSession>()
            .Include(x => x.Topic)
            .Include(x => x.Classroom)
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate >= start
                && x.SessionDate <= end)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(ct);

        var dtos = new List<TheoryClassSessionDto>();
        foreach (var s in sessions)
        {
            dtos.Add(await MapSessionAsync(s.Id, studentUserId, ct));
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
                .ToList());
    }

    public async Task CancelSessionAsync(
        int schoolUserId,
        int sessionId,
        int actorUserId,
        string? reason,
        CancellationToken ct)
    {
        var session = await RequireSessionAsync(schoolUserId, sessionId, ct);
        if (session.Status == TheoryClassStatuses.Cancelled)
        {
            return;
        }

        session.Status = TheoryClassStatuses.Cancelled;
        session.CancellationReason = reason;
        session.CancelledByUserId = actorUserId;
        session.CancelledAt = _clock.UtcNow;
        session.UpdatedAt = _clock.UtcNow;

        var reservations = await _db.Set<TheoryClassReservation>()
            .Where(x => x.ClassSessionId == sessionId
                && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var studentIds = new List<int>();
        foreach (var r in reservations)
        {
            r.Status = TheoryReservationStatuses.CancelledBySchool;
            r.CancelledAt = _clock.UtcNow;
            r.CancellationReason = reason;
            r.UpdatedAt = _clock.UtcNow;
            studentIds.Add(r.StudentUserId);
        }

        await _db.SaveChangesAsync(ct);
        if (studentIds.Count > 0)
        {
            await _notifications.NotifyUsersAsync(
                studentIds,
                "Clase teórica cancelada",
                $"La escuela canceló la clase del {session.SessionDate:dd/MM/yyyy} a las {session.StartTime:HH:mm}.",
                NotificationTypes.TheoryClass,
                null,
                "theory_class",
                session.Id,
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
        await ValidateNoScheduleConflictAsync(studentUserId, session, null, ct);

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

        var (hoursCompleted, absences) = await ComputeProgressAsync(studentUserId, ct);
        var hoursRequired = settings.RequiredTheoryHours;
        var progress = hoursRequired <= 0
            ? 0
            : Math.Round(hoursCompleted / hoursRequired * 100m, 1);

        var (currentStreak, bestStreak) = await ComputeStreaksAsync(studentUserId, ct);
        var checkedIn = await _db.Set<StudentDailyCheckIn>()
            .AnyAsync(x => x.StudentUserId == studentUserId && x.CheckInDate == today, ct);

        var (nextAction, opensAt, countdownLabel) = await ComputeNextActionAsync(
            studentUserId,
            schoolUserId,
            ct);

        var tasks = new List<TheoryDailyTaskDto>
        {
            new("Check-in diario", checkedIn),
            new("Revisar próxima clase", nextDto is not null),
            new("Reservar clase de mañana", false)
        };

        return new TheoryStudentDashboardDto(
            nextDto,
            upcomingDtos,
            progress,
            hoursCompleted,
            hoursRequired,
            Math.Max(0, upcoming.Count),
            absences,
            currentStreak,
            bestStreak,
            nextAction,
            countdownLabel,
            opensAt,
            checkedIn,
            tasks);
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

    // ── Enrollments ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<EnrollmentDto>> ListEnrollmentsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var items = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);
        var result = new List<EnrollmentDto>();
        foreach (var e in items)
        {
            var user = await _users.GetByIdAsync(e.StudentUserId, ct);
            result.Add(new EnrollmentDto(
                e.Id,
                e.StudentUserId,
                user?.Name ?? $"#{e.StudentUserId}",
                user?.Email ?? "",
                e.Status,
                e.CreatedAt,
                e.AcceptedAt));
        }

        return result;
    }

    public async Task<EnrollmentDto> UpdateEnrollmentAsync(
        int schoolUserId,
        int enrollmentId,
        UpdateEnrollmentRequest request,
        CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.Id == enrollmentId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Inscripción no encontrada.", "enrollment_not_found");

        var now = _clock.UtcNow;
        enrollment.Status = request.Status;
        enrollment.UpdatedAt = now;
        if (request.Status is StudentEnrollmentStatuses.Accepted or StudentEnrollmentStatuses.Active)
        {
            enrollment.AcceptedAt ??= now;
        }

        if (request.Status == StudentEnrollmentStatuses.Suspended)
        {
            enrollment.SuspendedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        var user = await _users.GetByIdAsync(enrollment.StudentUserId, ct);
        return new EnrollmentDto(
            enrollment.Id,
            enrollment.StudentUserId,
            user?.Name ?? "",
            user?.Email ?? "",
            enrollment.Status,
            enrollment.CreatedAt,
            enrollment.AcceptedAt);
    }

    public async Task EnsureEnrollmentForStudentAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var existing = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        if (existing is not null)
        {
            return;
        }

        var now = _clock.UtcNow;
        await _db.Set<SchoolStudentEnrollment>().AddAsync(new SchoolStudentEnrollment
        {
            SchoolUserId = schoolUserId,
            StudentUserId = studentUserId,
            Status = StudentEnrollmentStatuses.Active,
            CreatedAt = now,
            AcceptedAt = now,
            UpdatedAt = now
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private async Task<TheoryClassSessionDto> MapSessionAsync(
        int sessionId,
        int? studentUserId,
        CancellationToken ct)
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

        var (state, message) = ComputeBookingState(session, occupied, mine, _clock.UtcNow);
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
        DateTime nowUtc)
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
        int? ignoreReservationId,
        CancellationToken ct)
    {
        var existing = await _db.Set<TheoryClassReservation>()
            .Include(x => x.ClassSession)
            .Where(x => x.StudentUserId == studentUserId
                && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status)
                && (ignoreReservationId == null || x.Id != ignoreReservationId))
            .ToListAsync(ct);

        foreach (var r in existing)
        {
            var s = r.ClassSession;
            if (s is null)
            {
                continue;
            }

            if (ColombiaTime.TimesOverlap(
                    target.SessionDate, target.StartTime, target.EndTime,
                    s.SessionDate, s.StartTime, s.EndTime))
            {
                throw new DomainException(
                    "Ya tienes otra clase reservada en ese horario.",
                    400,
                    "schedule_conflict");
            }
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
        await EnsureEnrollmentForStudentAsync(schoolUserId, studentUserId, ct);
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        if (!StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status))
        {
            throw new ForbiddenException(
                "Tu escuela aún no te ha habilitado para reservar clases.",
                "enrollment_not_active");
        }
    }

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

    private async Task<(decimal Hours, int Absences)> ComputeProgressAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var records = await _db.Set<TheoryAttendanceRecord>()
            .Include(x => x.ClassSession)
            .Where(x => x.StudentUserId == studentUserId)
            .ToListAsync(ct);
        decimal hours = 0;
        var absences = 0;
        foreach (var r in records)
        {
            if (r.Status is TheoryAttendanceStatuses.Present or TheoryAttendanceStatuses.Late)
            {
                var s = r.ClassSession;
                if (s is not null)
                {
                    hours += (decimal)(s.EndTime - s.StartTime).TotalHours;
                }
            }
            else if (r.Status == TheoryAttendanceStatuses.Absent)
            {
                absences++;
            }
        }

        return (Math.Round(hours, 1), absences);
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
        CancellationToken ct)
    {
        var tomorrow = ColombiaTime.TodayInColombia().AddDays(1);
        var session = await _db.Set<TheoryClassSession>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate == tomorrow
                && x.Status == TheoryClassStatuses.Scheduled)
            .OrderBy(x => x.StartTime)
            .FirstOrDefaultAsync(ct);
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
            s.SaturdayEnabled,
            s.NotifyReservationOpen,
            s.NotifyClassReminder24h,
            s.NotifyClassReminder1h);

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
}
