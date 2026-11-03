using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>
/// Rule-based nudges/reminders/alerts (no LLM). Used by FormationNudgeBot,
/// PracticalReminderBot and SchoolOpsAlertBot hosted services.
/// </summary>
public sealed class FormationOpsBotRunner
{
    private readonly CaleDbContext _db;
    private readonly StudentFormationFacade _formation;
    private readonly SchoolOpsAlertService _opsAlerts;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<FormationOpsBotRunner> _logger;

    public FormationOpsBotRunner(
        CaleDbContext db,
        StudentFormationFacade formation,
        SchoolOpsAlertService opsAlerts,
        INotificationPublisher notifications,
        ILogger<FormationOpsBotRunner> logger)
    {
        _db = db;
        _formation = formation;
        _opsAlerts = opsAlerts;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>Students who can book but have no upcoming activity get one daily nudge.</summary>
    public async Task<int> RunFormationNudgesAsync(CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var day = today.ToString("yyyyMMdd");
        var studentIds = await _db.Set<SchoolStudentEnrollment>()
            .AsNoTracking()
            .Where(x => StudentEnrollmentStatuses.CanReserveStatuses.Contains(x.Status))
            .Select(x => x.StudentUserId)
            .Distinct()
            .OrderBy(id => id)
            .Take(400)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var studentId in studentIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var status = await _formation.GetStatusAsync(studentId, ct);
                if (!status.HasActiveSchool)
                {
                    continue;
                }

                var canBook = status.Stages.Any(s => s.CanBook);
                var hasUpcoming = status.Upcoming.Count > 0;
                if (!canBook || hasUpcoming)
                {
                    continue;
                }

                var action = status.NextRecommendedAction ?? "Revisa Mi formación para continuar.";
                var path = status.NextRecommendedPath ?? "/student/formacion";
                await _notifications.NotifyUsersAsync(
                    [studentId],
                    new NotificationDraft(
                        "Tu siguiente paso en la formación",
                        $"{status.CurrentStageLabel}: {action}",
                        NotificationTypes.Reminder,
                        RelatedEntity: "formation_nudge",
                        RelatedId: studentId,
                        Link: path,
                        Priority: NotificationPriorities.Normal,
                        DedupeKey: $"formation-nudge:{studentId}:{day}"),
                    ct);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Formation nudge skipped for student {StudentId}", studentId);
            }
        }

        return sent;
    }

    /// <summary>Remind students of practical lessons / exams in the next 36 hours.</summary>
    public async Task<int> RunPracticalRemindersAsync(CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var nowLocal = ColombiaTime.NowInColombia();
        var day = today.ToString("yyyyMMdd");
        var until = today.AddDays(1);

        var reservations = await _db.Set<PracticalLessonReservation>()
            .AsNoTracking()
            .Include(x => x.LessonSession)
            .Where(x => PracticalReservationStatuses.ActiveStatuses.Contains(x.Status)
                && x.LessonSession != null
                && x.LessonSession.Status != PracticalLessonStatuses.Cancelled
                && x.LessonSession.SessionDate >= today
                && x.LessonSession.SessionDate <= until)
            .OrderBy(x => x.LessonSession!.SessionDate)
            .ThenBy(x => x.LessonSession!.StartTime)
            .ThenBy(x => x.Id)
            .Take(500)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var r in reservations)
        {
            ct.ThrowIfCancellationRequested();
            var session = r.LessonSession!;
            var start = session.SessionDate.ToDateTime(session.StartTime);
            var hoursUntil = (start - nowLocal).TotalHours;
            if (hoursUntil is < 1 or > 36)
            {
                continue;
            }

            var kindLabel = PracticalSessionKinds.IsExam(session.Kind)
                ? "examen de manejo"
                : "clase de manejo";
            await _notifications.NotifyUsersAsync(
                [r.StudentUserId],
                new NotificationDraft(
                    $"Recordatorio: {kindLabel}",
                    $"Tienes {kindLabel} el {session.SessionDate:dd/MM/yyyy} a las {session.StartTime:HH:mm}. Abre Mi formación si necesitas reprogramar.",
                    NotificationTypes.Reminder,
                    RelatedEntity: "practical_reminder",
                    RelatedId: r.Id,
                    Link: "/student/formacion",
                    Priority: NotificationPriorities.High,
                    DedupeKey: $"practical-reminder:{r.Id}:{day}"),
                ct);
            sent++;
        }

        return sent;
    }

    /// <summary>Push high-priority ops alerts to each school once per day.</summary>
    public async Task<int> RunSchoolOpsAlertsAsync(CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var day = today.ToString("yyyyMMdd");
        var schoolIds = await _db.Set<SchoolStudentEnrollment>()
            .AsNoTracking()
            .Select(x => x.SchoolUserId)
            .Distinct()
            .OrderBy(id => id)
            .Take(200)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var schoolId in schoolIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var summary = await _opsAlerts.GetAlertsAsync(schoolId, ct);
                var high = summary.Alerts
                    .Where(a => a.Priority == "high" && a.Code != "ops_ok")
                    .OrderBy(a => a.Code, StringComparer.Ordinal)
                    .Take(3)
                    .ToList();
                if (high.Count == 0)
                {
                    continue;
                }

                foreach (var alert in high)
                {
                    await _notifications.NotifyUsersAsync(
                        [schoolId],
                        new NotificationDraft(
                            alert.Title,
                            alert.Detail + (string.IsNullOrWhiteSpace(alert.RecommendedAction)
                                ? ""
                                : $" {alert.RecommendedAction}"),
                            NotificationTypes.Admin,
                            RelatedEntity: "school_ops_alert",
                            RelatedId: schoolId,
                            Link: alert.ActionPath ?? "/school/agenda",
                            Priority: NotificationPriorities.High,
                            DedupeKey: $"school-ops:{schoolId}:{alert.Code}:{day}"),
                        ct);
                    sent++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "School ops alert skipped for school {SchoolId}", schoolId);
            }
        }

        return sent;
    }
}
