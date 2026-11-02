using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>
/// Rule-based school alerts (no LLM). Feeds agenda dashboard and Kazu Quick tools.
/// </summary>
public sealed class SchoolOpsAlertService
{
    private readonly CaleDbContext _db;
    private readonly TheoryTrainingService _theory;
    private readonly SchoolAgendaService _agenda;
    private readonly IClock _clock;

    public SchoolOpsAlertService(
        CaleDbContext db,
        TheoryTrainingService theory,
        SchoolAgendaService agenda,
        IClock clock)
    {
        _db = db;
        _theory = theory;
        _agenda = agenda;
        _clock = clock;
    }

    public async Task<SchoolOpsAlertSummaryDto> GetAlertsAsync(int schoolUserId, CancellationToken ct)
    {
        var alerts = new List<SchoolOpsAlertDto>();
        var today = ColombiaTime.TodayInColombia();
        var weekEnd = today.AddDays(6);

        var enrollments = await _db.Set<SchoolStudentEnrollment>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId
                && StudentEnrollmentStatuses.CanReserveStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var readyTheoryExam = 0;
        var authorizedNoBooking = 0;
        var readyPractical = 0;
        var stagnant = 0;

        foreach (var e in enrollments.Take(200))
        {
            try
            {
                var pe = await _theory.GetPracticalEligibilityAsync(schoolUserId, e.StudentUserId, ct);
                if (pe.TheoryHoursComplete && pe.WorkshopHoursComplete && pe.TheoryExamAuthorized && !pe.TheoryExamPassed)
                {
                    readyTheoryExam++;
                    var hasBooking = await _db.Set<TheoryExamAppointment>()
                        .AsNoTracking()
                        .AnyAsync(x => x.SchoolUserId == schoolUserId
                            && x.StudentUserId == e.StudentUserId
                            && x.Status == TheoryExamBookingStatuses.Active
                            && x.ExamDate >= today, ct);
                    if (!hasBooking)
                    {
                        authorizedNoBooking++;
                    }
                }

                if (pe.CanBookPractical)
                {
                    readyPractical++;
                }

                var lastActivity = e.AcceptedAt ?? e.CreatedAt;
                if ((_clock.UtcNow - lastActivity).TotalDays >= 14
                    && !pe.TheoryExamPassed
                    && pe.TheoryHoursCompleted < pe.TheoryHoursRequired)
                {
                    stagnant++;
                }
            }
            catch
            {
                /* skip student with data issues */
            }
        }

        var agenda = await _agenda.GetAsync(schoolUserId, today, weekEnd, ct);
        var practicalSlots = agenda.Items.Count(x => x.Kind == "practical");
        var practicalFree = agenda.Items
            .Where(x => x.Kind == "practical")
            .Sum(x => Math.Max(0, x.Available));
        var theoryFree = agenda.Items
            .Where(x => x.Kind == "theory")
            .Sum(x => Math.Max(0, x.Available));
        var examFree = agenda.Items
            .Where(x => x.Kind == "exam")
            .Sum(x => Math.Max(0, x.Available));

        if (authorizedNoBooking >= 3)
        {
            alerts.Add(new SchoolOpsAlertDto(
                "theory_exam_ready_no_booking",
                "high",
                "Estudiantes listos sin cita de examen teórico",
                $"{authorizedNoBooking} estudiantes autorizados y con horas aún no tienen reserva de examen.",
                "Abre cupos o asigna citas desde Exámenes teóricos.",
                "/school/theory-exams",
                authorizedNoBooking,
                examFree));
        }

        if (readyPractical >= 5 && practicalFree < readyPractical / 2)
        {
            alerts.Add(new SchoolOpsAlertDto(
                "practical_demand_exceeds_supply",
                "high",
                "Pocos cupos de manejo frente a la demanda",
                $"{readyPractical} estudiantes habilitados para manejo y solo {practicalFree} cupos libres esta semana ({practicalSlots} sesiones).",
                "Publica más horarios de manejo o duplica la semana.",
                "/school/practical",
                readyPractical,
                practicalFree));
        }

        if (theoryFree >= 20 && enrollments.Count > 0)
        {
            alerts.Add(new SchoolOpsAlertDto(
                "theory_low_occupation",
                "medium",
                "Cupos teóricos con baja ocupación",
                $"Hay {theoryFree} asientos libres en clases teóricas esta semana.",
                "Revisa la agenda o motiva a estudiantes pendientes a reservar.",
                "/school/agenda",
                theoryFree));
        }

        if (stagnant >= 5)
        {
            alerts.Add(new SchoolOpsAlertDto(
                "students_stagnant",
                "medium",
                "Estudiantes sin avanzar",
                $"{stagnant} estudiantes activos llevan 14+ días sin completar horas de teoría.",
                "Contacta al grupo o revisa saldos y autorizaciones.",
                "/school/students",
                stagnant));
        }

        if (alerts.Count == 0)
        {
            alerts.Add(new SchoolOpsAlertDto(
                "ops_ok",
                "low",
                "Operación estable",
                "No hay alertas críticas esta semana según cupos y habilitaciones.",
                "Sigue monitoreando la agenda semanal.",
                "/school/agenda"));
        }

        return new SchoolOpsAlertSummaryDto(
            _clock.UtcNow.ToString("O"),
            alerts.OrderByDescending(a => a.Priority switch
            {
                "high" => 3,
                "medium" => 2,
                _ => 1
            }).ToList());
    }
}
