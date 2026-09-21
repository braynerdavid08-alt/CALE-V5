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
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        await RequireSessionAsync(schoolUserId, sessionId, ct);

        var enrollment = await GetEnrollmentAsync(schoolUserId, request.StudentUserId, ct);
        if (!StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status))
        {
            throw new ForbiddenException(
                "El estudiante no está habilitado en esta escuela.",
                "student_not_enrolled");
        }

        var reservation = await _db.Set<TheoryClassReservation>()
            .FirstOrDefaultAsync(x => x.ClassSessionId == sessionId
                && x.StudentUserId == request.StudentUserId
                && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
        if (reservation is null)
        {
            throw new DomainException(
                "El estudiante no tiene reserva activa para esta clase.",
                400,
                "reservation_required");
        }

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

        reservation.Status = request.Status switch
        {
            TheoryAttendanceStatuses.Present or TheoryAttendanceStatuses.Late => TheoryReservationStatuses.Attended,
            TheoryAttendanceStatuses.Absent => TheoryReservationStatuses.NoShow,
            _ => reservation.Status
        };
        reservation.UpdatedAt = now;

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
}
