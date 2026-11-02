using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>
/// Aggregates existing theory / exam / practical dashboards into one formation status.
/// Does not recompute eligibility rules.
/// </summary>
public sealed class StudentFormationFacade
{
    private readonly TheoryTrainingService _theory;
    private readonly PracticalTrainingService _practical;
    private readonly TheoryExamScheduleService _exams;

    public StudentFormationFacade(
        TheoryTrainingService theory,
        PracticalTrainingService practical,
        TheoryExamScheduleService exams)
    {
        _theory = theory;
        _practical = practical;
        _exams = exams;
    }

    public async Task<StudentFormationStatusDto> GetStatusAsync(int studentUserId, CancellationToken ct)
    {
        TheoryStudentDashboardDto? theory = null;
        PracticalStudentDashboardDto? practical = null;
        StudentExamAvailabilityDto? exam = null;

        try
        {
            theory = await _theory.GetStudentDashboardAsync(studentUserId, ct);
        }
        catch
        {
            /* school / tables unavailable */
        }

        try
        {
            practical = await _practical.GetStudentDashboardAsync(studentUserId, ct);
        }
        catch
        {
            /* practical tables unavailable */
        }

        try
        {
            exam = await _exams.GetStudentAvailabilityAsync(studentUserId, null, null, ct);
        }
        catch
        {
            /* exam calendar unavailable */
        }

        var pe = theory?.PracticalEligibility ?? practical?.Eligibility;
        var balanceDue = theory?.BalanceDue ?? 0m;
        var hasSchool = theory is not null || practical is not null || exam is not null;

        var stages = BuildStages(theory, practical, exam, pe, balanceDue, hasSchool);
        var upcoming = BuildUpcoming(theory, practical, exam);
        var (currentId, currentLabel) = ResolveCurrent(stages);
        var (nextAction, nextPath) = ResolveNext(stages);

        return new StudentFormationStatusDto(
            currentId,
            currentLabel,
            nextAction,
            nextPath,
            stages,
            upcoming,
            pe,
            balanceDue,
            hasSchool);
    }

    private static IReadOnlyList<FormationStageDto> BuildStages(
        TheoryStudentDashboardDto? theory,
        PracticalStudentDashboardDto? practical,
        StudentExamAvailabilityDto? exam,
        PracticalEligibilityDto? pe,
        decimal balanceDue,
        bool hasSchool)
    {
        var stages = new List<FormationStageDto>();

        if (!hasSchool)
        {
            stages.Add(new FormationStageDto(
                FormationStageIds.Enrollment,
                "Inscripción en la escuela",
                FormationStageStatuses.Blocked,
                "Vincula tu cuenta a una escuela CEA para ver tu formación.",
                "Sin escuela vinculada.",
                false,
                "Buscar escuela",
                "/student/school"));
            return stages;
        }

        stages.Add(new FormationStageDto(
            FormationStageIds.Enrollment,
            "Inscripción y habilitación",
            FormationStageStatuses.Completed,
            balanceDue > 0
                ? $"Activo · saldo pendiente {balanceDue:N0} COP"
                : "Matrícula activa en tu escuela.",
            balanceDue > 0 ? "Tienes un saldo pendiente con la escuela." : null,
            false,
            null,
            null));

        var theoryHoursDone = pe?.TheoryHoursComplete == true
            || (theory is not null && theory.HoursCompleted >= theory.HoursRequired);
        var workshopDone = pe?.WorkshopHoursComplete == true
            || (theory is not null && theory.WorkshopHoursCompleted >= theory.WorkshopHoursRequired);
        var theoryComplete = theoryHoursDone && workshopDone;
        var theoryHours = pe?.TheoryHoursCompleted ?? theory?.HoursCompleted ?? 0;
        var theoryReq = pe?.TheoryHoursRequired ?? theory?.HoursRequired ?? 0;
        var workshopHours = pe?.WorkshopHoursCompleted ?? theory?.WorkshopHoursCompleted ?? 0;
        var workshopReq = pe?.WorkshopHoursRequired ?? theory?.WorkshopHoursRequired ?? 0;
        var hasTheoryReservation = theory?.UpcomingReservations.Count > 0 || theory?.NextClass is not null;

        string theoryStatus;
        string? theoryBlock = null;
        if (theoryComplete)
        {
            theoryStatus = FormationStageStatuses.Completed;
        }
        else if (hasTheoryReservation)
        {
            theoryStatus = FormationStageStatuses.Reserved;
        }
        else if (balanceDue > 0)
        {
            theoryStatus = FormationStageStatuses.Blocked;
            theoryBlock = "Tienes un saldo pendiente.";
        }
        else
        {
            theoryStatus = FormationStageStatuses.InProgress;
        }

        stages.Add(new FormationStageDto(
            FormationStageIds.Theory,
            "Formación teórica",
            theoryStatus,
            $"Teoría {theoryHours:0.#}/{theoryReq:0.#} h · Taller {workshopHours:0.#}/{workshopReq:0.#} h",
            theoryBlock,
            !theoryComplete && balanceDue <= 0,
            theoryComplete ? null : "Agendar teoría",
            theoryComplete ? null : "/student/training",
            theoryHours,
            theoryReq));

        var examPassed = pe?.TheoryExamPassed == true;
        var examAuthorized = pe?.TheoryExamAuthorized == true;
        var hasExamBooking = exam?.MyBooking is not null;
        var canBookExam = exam?.CanBook == true;

        string examStatus;
        string? examBlock = exam?.BlockReason;
        if (examPassed)
        {
            examStatus = FormationStageStatuses.Completed;
            examBlock = null;
        }
        else if (hasExamBooking)
        {
            examStatus = FormationStageStatuses.Reserved;
        }
        else if (canBookExam)
        {
            examStatus = FormationStageStatuses.AvailableToBook;
        }
        else if (!examAuthorized && theoryComplete)
        {
            examStatus = FormationStageStatuses.NeedsAuthorization;
            examBlock ??= "Tu escuela debe autorizarte para el examen teórico.";
        }
        else if (!theoryComplete)
        {
            examStatus = FormationStageStatuses.Blocked;
            examBlock ??= "Completa primero las horas de teoría y taller.";
        }
        else
        {
            examStatus = FormationStageStatuses.Blocked;
        }

        stages.Add(new FormationStageDto(
            FormationStageIds.TheoryExam,
            "Examen teórico",
            examStatus,
            examPassed
                ? "Aprobado."
                : hasExamBooking
                    ? $"Reservado: {exam!.MyBooking!.Date} {exam.MyBooking.Time}"
                    : "Presenta el examen oficial de tu escuela.",
            examBlock,
            canBookExam,
            examPassed ? null : (canBookExam || hasExamBooking ? "Ver examen" : null),
            examPassed ? null : "/student/exam"));

        var practicalAuth = pe?.PracticalAuthorized == true || pe?.CanBookPractical == true;
        var lessonsDone = practical?.CompletedLessons ?? 0;
        var lessonsReq = practical?.PracticeRequiredLessons > 0
            ? practical.PracticeRequiredLessons
            : practical?.RequiredLessons ?? 0;
        var hasPracticalReservation = practical?.UpcomingReservations.Any(x =>
            !string.Equals(x.Kind, PracticalSessionKinds.Exam, StringComparison.OrdinalIgnoreCase)) == true;

        string practicalStatus;
        string? practicalBlock = pe?.BlockReason;
        if (!examPassed)
        {
            practicalStatus = FormationStageStatuses.Blocked;
            practicalBlock ??= "Aprueba el examen teórico antes del manejo.";
        }
        else if (!practicalAuth)
        {
            practicalStatus = FormationStageStatuses.NeedsAuthorization;
            practicalBlock ??= "Tu escuela debe autorizarte para clases de manejo.";
        }
        else if (lessonsReq > 0 && lessonsDone >= lessonsReq)
        {
            practicalStatus = FormationStageStatuses.Completed;
            practicalBlock = null;
        }
        else if (hasPracticalReservation)
        {
            practicalStatus = FormationStageStatuses.Reserved;
            practicalBlock = null;
        }
        else if (practicalAuth)
        {
            practicalStatus = FormationStageStatuses.AvailableToBook;
            practicalBlock = null;
        }
        else
        {
            practicalStatus = FormationStageStatuses.Blocked;
        }

        stages.Add(new FormationStageDto(
            FormationStageIds.Practical,
            "Formación práctica (manejo)",
            practicalStatus,
            lessonsReq > 0
                ? $"Clases {lessonsDone}/{lessonsReq}"
                : "Clases de manejo con instructor y vehículo.",
            practicalBlock,
            practicalAuth && examPassed && lessonsDone < Math.Max(lessonsReq, 1),
            practicalAuth && examPassed ? "Agendar manejo" : null,
            "/student/practical",
            LessonsCompleted: lessonsDone,
            LessonsRequired: lessonsReq));

        var canBookPracticalExam = practical?.CanBookPracticalExam == true;
        var examScheduled = practical?.PracticalExamScheduled == true;
        var examAttended = practical?.PracticalExamAttended == true;
        var practicalExamBlock = practical?.PracticalExamBlockReason;

        string pExamStatus;
        if (examAttended)
        {
            pExamStatus = FormationStageStatuses.Completed;
        }
        else if (examScheduled)
        {
            pExamStatus = FormationStageStatuses.Reserved;
        }
        else if (canBookPracticalExam)
        {
            pExamStatus = FormationStageStatuses.AvailableToBook;
        }
        else
        {
            pExamStatus = FormationStageStatuses.Blocked;
        }

        stages.Add(new FormationStageDto(
            FormationStageIds.PracticalExam,
            "Examen de manejo",
            pExamStatus,
            examAttended
                ? "Examen de manejo registrado."
                : examScheduled
                    ? "Examen de manejo agendado."
                    : "Última hora del programa (1 h).",
            examAttended ? null : practicalExamBlock,
            canBookPracticalExam,
            canBookPracticalExam || examScheduled ? "Ver examen de manejo" : null,
            "/student/practical"));

        if (examAttended)
        {
            stages.Add(new FormationStageDto(
                FormationStageIds.Completed,
                "Formación completada",
                FormationStageStatuses.Completed,
                "Completaste el recorrido de programación en Luz Verde. ¡Felicitaciones!",
                null,
                false,
                null,
                null));
        }

        return stages;
    }

    private static IReadOnlyList<FormationUpcomingDto> BuildUpcoming(
        TheoryStudentDashboardDto? theory,
        PracticalStudentDashboardDto? practical,
        StudentExamAvailabilityDto? exam)
    {
        var list = new List<FormationUpcomingDto>();

        if (theory?.NextClass is { } nc)
        {
            list.Add(new FormationUpcomingDto(
                "theory",
                string.IsNullOrWhiteSpace(nc.TopicName) ? "Clase teórica" : nc.TopicName,
                nc.SessionDate.ToString("yyyy-MM-dd"),
                nc.StartTime,
                nc.EndTime,
                "/student/training"));
        }

        if (exam?.MyBooking is { } booking)
        {
            list.Add(new FormationUpcomingDto(
                "theory_exam",
                "Examen teórico",
                booking.Date,
                booking.Time,
                null,
                "/student/exam"));
        }

        if (practical?.UpcomingReservations is { Count: > 0 } upcoming)
        {
            foreach (var u in upcoming.Take(4))
            {
                var isExam = string.Equals(u.Kind, PracticalSessionKinds.Exam, StringComparison.OrdinalIgnoreCase);
                list.Add(new FormationUpcomingDto(
                    isExam ? "practical_exam" : "practical",
                    isExam ? "Examen de manejo" : (u.VehicleLabel ?? "Clase de manejo"),
                    u.SessionDate,
                    u.StartTime,
                    u.EndTime,
                    "/student/practical"));
            }
        }

        return list
            .OrderBy(x => x.Date, StringComparer.Ordinal)
            .ThenBy(x => x.StartTime, StringComparer.Ordinal)
            .Take(8)
            .ToList();
    }

    private static (string Id, string Label) ResolveCurrent(IReadOnlyList<FormationStageDto> stages)
    {
        var open = stages.FirstOrDefault(s =>
            s.Id != FormationStageIds.Enrollment
            && s.Status is FormationStageStatuses.InProgress
                or FormationStageStatuses.AvailableToBook
                or FormationStageStatuses.Reserved
                or FormationStageStatuses.NeedsAuthorization
                or FormationStageStatuses.Blocked);
        if (open is not null)
        {
            return (open.Id, open.Title);
        }

        var last = stages.LastOrDefault();
        return last is null
            ? (FormationStageIds.Enrollment, "Inscripción")
            : (last.Id, last.Title);
    }

    private static (string? Action, string? Path) ResolveNext(IReadOnlyList<FormationStageDto> stages)
    {
        var actionable = stages.FirstOrDefault(s =>
            s.Status is FormationStageStatuses.AvailableToBook or FormationStageStatuses.InProgress
            && !string.IsNullOrWhiteSpace(s.CtaPath));
        if (actionable is not null)
        {
            return (
                actionable.CtaLabel ?? $"Continuar con {actionable.Title.ToLowerInvariant()}",
                actionable.CtaPath);
        }

        var reserved = stages.FirstOrDefault(s => s.Status == FormationStageStatuses.Reserved);
        if (reserved is not null)
        {
            return ($"Tienes {reserved.Title.ToLowerInvariant()} reservada. Revisa el detalle.", reserved.CtaPath);
        }

        var blocked = stages.FirstOrDefault(s =>
            s.Status is FormationStageStatuses.Blocked or FormationStageStatuses.NeedsAuthorization
            && !string.IsNullOrWhiteSpace(s.BlockReason));
        if (blocked is not null)
        {
            return (blocked.BlockReason, blocked.CtaPath);
        }

        return ("Vas al día con tu formación.", null);
    }
}
