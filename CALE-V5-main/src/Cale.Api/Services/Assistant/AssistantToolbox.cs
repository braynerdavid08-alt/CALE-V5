using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Assistant;

public sealed record AssistantUser(int UserId, string Role, string Name, int? SchoolUserId);

public sealed record AssistantToolDefinition(
    string Name,
    string Description,
    JsonObject Parameters,
    bool RequiresConfirmation);

/// <summary>Bad input from the model (unknown id, wrong date…); the text goes back to the model.</summary>
public sealed class AssistantToolException : Exception
{
    public AssistantToolException(string message) : base(message) { }
}

public interface IAssistantToolbox
{
    IReadOnlyList<AssistantToolDefinition> ToolsFor(string role);

    /// <summary>Runs a read-only tool and returns compact text for the model.</summary>
    Task<string> ReadAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct);

    /// <summary>
    /// Same as <see cref="ReadAsync"/>, plus an optional structured card for the chat UI
    /// (exam availability). Text stays for the model; the UI prefers the card when present.
    /// </summary>
    Task<(string Text, AssistantExamAvailabilityUi? ExamAvailability)> ReadWithUiAsync(
        AssistantUser user, string tool, JsonObject args, CancellationToken ct);

    /// <summary>Validates a write tool and returns the confirmation text shown to the user (built from real data).</summary>
    Task<string> DescribeAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct);

    /// <summary>Executes a confirmed write tool through the regular services and returns the result for the user.</summary>
    Task<string> ExecuteAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct);
}

/// <summary>
/// Assistant tools mapped onto the same application services the screens use, so every
/// business rule (cupos, saldo, autorizaciones, horarios) still applies.
/// </summary>
public sealed class AssistantToolbox : IAssistantToolbox
{
    private readonly CaleDbContext _db;
    private readonly IUserStore _users;
    private readonly TheoryTrainingService _theory;
    private readonly PracticalTrainingService _practical;
    private readonly TheoryExamScheduleService _exams;
    private readonly ApprenticeRegistryService _registry;
    private readonly SchoolAgendaService _agenda;

    public AssistantToolbox(
        CaleDbContext db,
        IUserStore users,
        TheoryTrainingService theory,
        PracticalTrainingService practical,
        TheoryExamScheduleService exams,
        ApprenticeRegistryService registry,
        SchoolAgendaService agenda)
    {
        _db = db;
        _users = users;
        _theory = theory;
        _practical = practical;
        _exams = exams;
        _registry = registry;
        _agenda = agenda;
    }

    // ── Definitions ─────────────────────────────────────────────────────

    private static readonly AssistantToolDefinition[] StudentTools =
    [
        Read("mi_progreso", "Progreso del estudiante: horas de teoría y taller, faltas, reservas, examen teórico, clases de manejo y saldo."),
        Read("clases_teoricas", "Clases teóricas de una semana con cupos y si el estudiante puede reservar.",
            P("semana_inicio", "string", "Lunes de la semana en formato AAAA-MM-DD. Vacío = semana actual."),
            P("completo", "boolean", "true para listar todas las clases de la semana; si no, un resumen razonable.")),
        Write("reservar_clase_teorica", "Prepara la reserva de una clase teórica (el usuario debe confirmar).",
            P("clase_id", "integer", "clase_id de la lista de clases teóricas.", true)),
        Write("cancelar_reserva_teorica", "Prepara la cancelación de una reserva de clase teórica.",
            P("reserva_id", "integer", "reserva_id de la clase reservada.", true)),
        Read("cupos_examen", "Cupos disponibles para el examen teórico y la cita actual del estudiante.",
            P("desde", "string", "Fecha inicial AAAA-MM-DD (opcional)."),
            P("hasta", "string", "Fecha final AAAA-MM-DD (opcional)."),
            P("completo", "boolean", "true para listar todos los días con cupo.")),
        Write("agendar_examen", "Prepara la cita del examen teórico en una fecha y hora con cupo.",
            P("fecha", "string", "Fecha AAAA-MM-DD.", true),
            P("hora", "string", "Hora HH:mm tal como aparece en los cupos.", true)),
        Write("cancelar_examen", "Prepara la cancelación de la cita de examen teórico.",
            P("cita_id", "integer", "cita_id de la cita actual.", true)),
        Read("clases_manejo", "Clases y examen de manejo: progreso (N-1 clases + examen), cupos y si ya puede agendar el examen.",
            P("completo", "boolean", "true para listar todos los cupos publicados.")),
        Write("reservar_clase_manejo", "Prepara la reserva de una clase de manejo (no examen).",
            P("clase_id", "integer", "clase_id de tipo Clase en la lista de clases_manejo.", true)),
        Write("reservar_examen_manejo", "Prepara la reserva del examen de manejo (última hora del programa). Solo si ya completó todas las clases menos una.",
            P("clase_id", "integer", "clase_id de tipo Examen en la lista de clases_manejo.", true)),
        Write("cancelar_reserva_manejo", "Prepara la cancelación de una clase o examen de manejo reservado.",
            P("reserva_id", "integer", "reserva_id de la clase o examen de manejo.", true))
    ];

    private static readonly AssistantToolDefinition[] SchoolTools =
    [
        Read("resumen_escuela", "Resumen general: estudiantes, saldos, listos para examen o manejo, exámenes próximos."),
        Read("progreso_estudiantes", "Progreso de los estudiantes (horas, examen, manejo, saldo). Se puede filtrar o buscar por nombre.",
            P("filtro", "string", "todos | listos_examen | autorizados_examen | listos_manejo | en_manejo | con_saldo | pendientes | faltan_horas"),
            P("buscar", "string", "Parte del nombre o correo del estudiante."),
            P("completo", "boolean", "true para listar todos (hasta 60); si no, un resumen con los primeros.")),
        Read("detalle_estudiante", "Expediente de un estudiante: categoría, horas, examen, práctica, saldo y autorizaciones.",
            P("estudiante_id", "integer", "estudiante_id de la lista de progreso.", true)),
        Read("agenda_escuela", "Agenda de clases teóricas, exámenes y clases de manejo en un rango de fechas.",
            P("desde", "string", "Fecha inicial AAAA-MM-DD (opcional, por defecto esta semana)."),
            P("hasta", "string", "Fecha final AAAA-MM-DD (opcional)."),
            P("completo", "boolean", "true para listar todas las actividades del rango.")),
        Read("cupos_examen_escuela", "Cupos libres del calendario de exámenes teóricos.",
            P("desde", "string", "Fecha inicial AAAA-MM-DD (opcional)."),
            P("hasta", "string", "Fecha final AAAA-MM-DD (opcional)."),
            P("completo", "boolean", "true para listar todos los días con cupo.")),
        Read("opciones_manejo", "Vehículos, instructores y estudiantes habilitados para asignar clases de manejo.",
            P("completo", "boolean", "true para listar todos los estudiantes autorizados.")),
        Write("autorizar_examen", "Prepara habilitar (o quitar) el examen teórico de un estudiante.",
            P("estudiante_id", "integer", "estudiante_id.", true),
            P("autorizar", "boolean", "true para habilitar, false para quitar.", true)),
        Write("autorizar_manejo", "Prepara habilitar (o quitar) las clases de manejo de un estudiante.",
            P("estudiante_id", "integer", "estudiante_id.", true),
            P("autorizar", "boolean", "true para habilitar, false para quitar.", true)),
        Write("agendar_examen_estudiante", "Prepara la cita de examen teórico de un estudiante en un cupo libre.",
            P("estudiante_id", "integer", "estudiante_id.", true),
            P("fecha", "string", "Fecha AAAA-MM-DD.", true),
            P("hora", "string", "Hora HH:mm del cupo.", true)),
        Write("asignar_clase_manejo", "Prepara asignar una clase de manejo a un estudiante habilitado.",
            P("estudiante_id", "integer", "estudiante_id.", true),
            P("fecha", "string", "Fecha AAAA-MM-DD (no domingos).", true),
            P("hora_inicio", "string", "HH:mm", true),
            P("hora_fin", "string", "HH:mm", true),
            P("instructor_id", "integer", "instructor_id de opciones_manejo.", true),
            P("vehiculo_id", "integer", "vehiculo_id de opciones_manejo.", true)),
        Write("asignar_examen_manejo", "Prepara asignar el examen de manejo (1 h) a un estudiante que ya completó N-1 clases.",
            P("estudiante_id", "integer", "estudiante_id listo para examen de manejo.", true),
            P("fecha", "string", "Fecha AAAA-MM-DD (no domingos).", true),
            P("hora_inicio", "string", "HH:mm inicio (duración 1 h).", true),
            P("instructor_id", "integer", "instructor_id de opciones_manejo.", true),
            P("vehiculo_id", "integer", "vehiculo_id de opciones_manejo.", true))
    ];

    public IReadOnlyList<AssistantToolDefinition> ToolsFor(string role) => role switch
    {
        Roles.Student => StudentTools,
        Roles.School => SchoolTools,
        _ => []
    };

    // ── Reads ───────────────────────────────────────────────────────────

    public async Task<string> ReadAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct)
    {
        var (text, _) = await ReadWithUiAsync(user, tool, args, ct);
        return text;
    }

    public async Task<(string Text, AssistantExamAvailabilityUi? ExamAvailability)> ReadWithUiAsync(
        AssistantUser user, string tool, JsonObject args, CancellationToken ct)
    {
        EnsureAllowed(user, tool, write: false);
        return tool switch
        {
            "mi_progreso" => (await MyProgressAsync(user, ct), null),
            "clases_teoricas" => await TheoryWeekAsync(user, OptDate(args, "semana_inicio"), WantsFullList(args), ct),
            "cupos_examen" => await StudentExamSlotsRichAsync(user, OptDate(args, "desde"), OptDate(args, "hasta"), WantsFullList(args), ct),
            "clases_manejo" => await StudentPracticalAsync(user, WantsFullList(args), ct),
            "resumen_escuela" => (await SchoolSummaryAsync(user, ct), null),
            "progreso_estudiantes" => (await StudentsProgressAsync(user, Str(args, "filtro"), Str(args, "buscar"), WantsFullList(args), ct), null),
            "detalle_estudiante" => (await StudentDetailAsync(user, ReqInt(args, "estudiante_id"), ct), null),
            "agenda_escuela" => (await SchoolAgendaAsync(user, OptDate(args, "desde"), OptDate(args, "hasta"), WantsFullList(args), ct), null),
            "cupos_examen_escuela" => await SchoolExamSlotsRichAsync(user, OptDate(args, "desde"), OptDate(args, "hasta"), WantsFullList(args), ct),
            "opciones_manejo" => (await PracticalOptionsAsync(user, WantsFullList(args), ct), null),
            _ => throw new AssistantToolException($"Herramienta desconocida: {tool}.")
        };
    }

    private async Task<string> MyProgressAsync(AssistantUser user, CancellationToken ct)
    {
        var d = await _theory.GetStudentDashboardAsync(user.UserId, ct);
        var sb = new StringBuilder();

        sb.AppendLine("**Tu formación**");
        sb.AppendLine($"• Teoría: {Hours(d.HoursCompleted)} de {Hours(d.HoursRequired)} h");
        sb.AppendLine($"• Taller: {Hours(d.WorkshopHoursCompleted)} de {Hours(d.WorkshopHoursRequired)} h");
        sb.AppendLine($"• Faltas: {d.Absences}");
        sb.AppendLine($"• Grupo: {DayType(d.AttendanceDayType)}");
        sb.AppendLine($"• Saldo pendiente: {Money(d.BalanceDue)}");

        // Practical hours live in the practical module (A2=14+1, B1=19+1, C1=29+1).
        try
        {
            var (schoolId, _) = await _theory.ResolveStudentSchoolPublicAsync(user.UserId, ct);
            var enrollment = await _db.Set<SchoolStudentEnrollment>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.SchoolUserId == schoolId && x.StudentUserId == user.UserId,
                    ct);
            var categories = enrollment?.LicenseCategories;
            var practiceReq = PracticalLessonRequirements.GetPracticeRequired(categories);
            var programReq = PracticalLessonRequirements.GetRequired(categories);
            var summary = await _practical.GetApprenticePracticalSummaryAsync(schoolId, user.UserId, ct);
            var completed = summary.CompletedLessons;
            var categoryLabel = PracticalLessonRequirements.PrimaryCategory(categories);
            var remainingPractice = Math.Max(0, practiceReq - completed);

            sb.AppendLine();
            sb.AppendLine("**Manejo**");
            sb.AppendLine($"• Categoría: {categoryLabel}");
            sb.AppendLine($"• Clases formativas: {completed} de {practiceReq} h");
            sb.AppendLine($"• Examen de manejo: 1 h (programa total {programReq} h)");
            sb.AppendLine(remainingPractice > 0
                ? $"• Te faltan {remainingPractice} h de clase antes del examen"
                : "• Ya completaste las clases formativas; sigue el examen de manejo");
        }
        catch
        {
            // Practical tables may be unavailable; theory progress still shows.
        }

        if (d.NextClass is { } next)
        {
            sb.AppendLine();
            sb.AppendLine("**Próxima clase teórica**");
            sb.AppendLine($"• {next.TopicName}");
            sb.AppendLine($"• {Day(next.SessionDate)} a las {next.StartTime}");
        }

        var reservations = d.UpcomingReservations.Take(10).ToList();
        if (reservations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Tus reservas teóricas**");
            foreach (var r in reservations)
            {
                sb.AppendLine($"• {r.TopicName} — {Day(r.SessionDate)} a las {r.StartTime} (reserva_id={r.MyReservationId})");
            }
        }

        var e = d.PracticalEligibility;
        if (e is not null)
        {
            sb.AppendLine();
            sb.AppendLine("**Examen teórico y autorizaciones**");
            sb.AppendLine($"• Examen teórico: {AuthorizedLabel(e.TheoryExamAuthorized)}, {(e.TheoryExamPassed ? "ya aprobado" : "aún no aprobado")}");
            sb.AppendLine($"• Clases de manejo: {AuthorizedLabel(e.PracticalAuthorized)}, {(e.CanBookPractical ? "puedes reservar" : "aún no puedes reservar")}");
            if (!string.IsNullOrWhiteSpace(e.BlockReason))
            {
                sb.AppendLine($"• Lo que falta: {e.BlockReason}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("**Cita de examen teórico**");
        sb.AppendLine(d.NextExamAppointment is { } exam
            ? $"• Agendada: {exam.ExamDate} a las {exam.SlotTime} (cita_id={exam.Id})"
            : "• Todavía no tienes cita");

        if (!string.IsNullOrWhiteSpace(d.NextAction))
        {
            sb.AppendLine();
            sb.AppendLine("**Qué te conviene hacer**");
            sb.AppendLine($"• {d.NextAction}");
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<(string Text, AssistantExamAvailabilityUi Ui)> TheoryWeekAsync(
        AssistantUser user, DateOnly? weekStart, bool fullList, CancellationToken ct)
    {
        var w = await _theory.GetStudentWeekScheduleAsync(user.UserId, weekStart, ct);
        var text = AssistantReplyFormatter.FormatTheoryWeek(
            w.WeekStart,
            w.WeekEnd,
            DayType(w.StudentAttendanceDayType),
            w.Sessions,
            fullList);
        return (text, BuildTheoryWeekUi(w, fullList));
    }

    public static AssistantExamAvailabilityUi BuildTheoryWeekUi(
        TheoryWeekScheduleDto w, bool fullList)
    {
        var sessions = w.Sessions
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.StartTime)
            .ToList();
        var take = AssistantReplyFormatter.TakeCount(
            sessions.Count, fullList, AssistantReplyFormatter.AgendaPreview);
        var shown = sessions.Take(take).ToList();
        var bookable = shown.Any(s =>
            s.MyReservationId is null
            && string.Equals(s.BookingState, "can_reserve", StringComparison.OrdinalIgnoreCase)
            && s.AvailableSeats > 0);

        var days = shown
            .GroupBy(s => s.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Select(g => new AssistantExamDayUi(
                g.Key,
                g.Select(s => new AssistantExamSlotUi(
                    Time: s.StartTime,
                    AvailableCapacity: Math.Max(0, s.AvailableSeats),
                    EndTime: s.EndTime,
                    Detail: $"{s.TopicName} · {s.ClassroomName}"
                        + (s.MyReservationId is not null ? " · ya reservada" : ""),
                    SessionId: s.MyReservationId is null
                        && string.Equals(s.BookingState, "can_reserve", StringComparison.OrdinalIgnoreCase)
                        && s.AvailableSeats > 0
                            ? s.Id
                            : null,
                    BookTool: s.MyReservationId is null
                        && string.Equals(s.BookingState, "can_reserve", StringComparison.OrdinalIgnoreCase)
                        && s.AvailableSeats > 0
                            ? "reservar_clase_teorica"
                            : null))
                    .ToList()))
            .Where(d => d.AvailableSlots.Count > 0)
            .ToList();

        return new AssistantExamAvailabilityUi(
            Type: "theory_week",
            Title: "Clases teóricas de la semana",
            DateRangeFrom: w.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateRangeTo: w.WeekEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CanBook: bookable,
            BlockReason: days.Count == 0 ? "No hay clases teóricas publicadas esa semana." : null,
            MyBookingSummary: $"Tu grupo: {DayType(w.StudentAttendanceDayType)}.",
            Selectable: bookable,
            Days: days);
    }

    private async Task<(string Text, AssistantExamAvailabilityUi Ui)> StudentExamSlotsRichAsync(
        AssistantUser user, DateOnly? from, DateOnly? to, bool fullList, CancellationToken ct)
    {
        var a = await _exams.GetStudentAvailabilityAsync(user.UserId, from, to, ct);
        string? booking = a.MyBooking is { } b
            ? $"Cita actual: {AssistantReplyFormatter.DayTitle(b.Date)} a las {b.Time} ({Status(b.Status)}). Puede cancelar: {YesNo(b.CanCancel)} (mínimo {a.MinCancelHours} h antes). (cita_id={b.Id})"
            : null;
        var days = a.Days
            .Select(d => (d.Date, (IReadOnlyList<(string Time, int Available)>)d.Slots
                .Where(s => s.Available > 0)
                .Select(s => (s.Time, s.Available))
                .ToList()))
            .ToList();
        var text = AssistantReplyFormatter.FormatExamSlotsText(
            a.From, a.To, a.CanBook, a.BlockReason, booking, days, fullList);
        return (text, BuildStudentExamUi(a));
    }

    public static AssistantExamAvailabilityUi BuildStudentExamUi(StudentExamAvailabilityDto a)
    {
        var days = a.Days
            .Select(d => new AssistantExamDayUi(
                d.Date,
                d.Slots
                    .Where(s => s.Available > 0)
                    .Select(s => new AssistantExamSlotUi(s.Time, s.Available))
                    .ToList()))
            .Where(d => d.AvailableSlots.Count > 0)
            .ToList();

        string? booking = a.MyBooking is { } b
            ? $"{DayIso(b.Date)} a las {b.Time} ({Status(b.Status)})"
            : null;

        return new AssistantExamAvailabilityUi(
            Type: "exam_availability",
            Title: "Cupos disponibles para el examen",
            DateRangeFrom: a.From,
            DateRangeTo: a.To,
            CanBook: a.CanBook,
            BlockReason: a.BlockReason,
            MyBookingSummary: booking,
            Selectable: a.CanBook && days.Count > 0,
            Days: days);
    }

    private async Task<(string Text, AssistantExamAvailabilityUi Ui)> StudentPracticalAsync(
        AssistantUser user, bool fullList, CancellationToken ct)
    {
        var d = await _practical.GetStudentDashboardAsync(user.UserId, ct);

        // Prefer explicit practice/program totals; fall back to category defaults if the
        // dashboard returned zeros (e.g. catch path after a practical-schema hiccup).
        var practiceReq = d.PracticeRequiredLessons > 0
            ? d.PracticeRequiredLessons
            : Math.Max(0, d.RequiredLessons - 1);
        var programReq = d.RequiredLessons;
        string? categoryLabel = null;
        try
        {
            var (schoolId, _) = await _theory.ResolveStudentSchoolPublicAsync(user.UserId, ct);
            var enrollment = await _db.Set<SchoolStudentEnrollment>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.SchoolUserId == schoolId && x.StudentUserId == user.UserId,
                    ct);
            categoryLabel = PracticalLessonRequirements.PrimaryCategory(enrollment?.LicenseCategories);
            if (practiceReq <= 0 || programReq <= 0)
            {
                programReq = PracticalLessonRequirements.GetRequired(enrollment?.LicenseCategories);
                practiceReq = PracticalLessonRequirements.GetPracticeRequired(enrollment?.LicenseCategories);
            }

            if (d.CompletedLessons == 0 && d.RequiredLessons == 0)
            {
                var summary = await _practical.GetApprenticePracticalSummaryAsync(schoolId, user.UserId, ct);
                d = d with
                {
                    CompletedLessons = summary.CompletedLessons,
                    RequiredLessons = summary.RequiredLessons > 0 ? summary.RequiredLessons : programReq,
                    PracticeRequiredLessons = practiceReq
                };
            }
        }
        catch
        {
            if (practiceReq <= 0)
            {
                practiceReq = PracticalLessonRequirements.GetPracticeRequired(null);
                programReq = PracticalLessonRequirements.GetRequired(null);
            }
        }

        var notes = new List<string>();
        if (!d.Eligibility.CanBookPractical && !string.IsNullOrWhiteSpace(d.Eligibility.BlockReason))
        {
            notes.Add($"Motivo clases: {d.Eligibility.BlockReason}");
        }

        if (!d.CanBookPracticalExam && !string.IsNullOrWhiteSpace(d.PracticalExamBlockReason))
        {
            notes.Add($"Motivo examen: {d.PracticalExamBlockReason}");
        }

        if (d.PracticalExamScheduled)
        {
            notes.Add("Ya tienes un examen de manejo agendado.");
        }

        if (d.PracticalExamAttended)
        {
            notes.Add("Ya presentaste el examen de manejo.");
        }

        var reservations = d.UpcomingReservations
            .Where(r => r.MyReservationId is int)
            .Take(10)
            .Select(r => (
                r.MyReservationId!.Value,
                string.Equals(r.Kind, "Exam", StringComparison.OrdinalIgnoreCase) ? "Examen" : "Clase",
                r.SessionDate,
                r.StartTime,
                r.EndTime,
                r.InstructorName,
                r.VehicleLabel))
            .ToList();
        var lessons = d.AvailableLessons
            .Where(l => !string.Equals(l.Kind, "Exam", StringComparison.OrdinalIgnoreCase))
            .Select(l => (l.Id, "Clase", l.SessionDate, l.StartTime, l.EndTime, l.InstructorName, l.VehicleLabel, l.BookingState ?? ""))
            .ToList();
        var exams = d.AvailableLessons
            .Where(l => string.Equals(l.Kind, "Exam", StringComparison.OrdinalIgnoreCase))
            .Select(l => (l.Id, "Examen", l.SessionDate, l.StartTime, l.EndTime, l.InstructorName, l.VehicleLabel, l.BookingState ?? ""))
            .ToList();

        var progressLine =
            $"Progreso: {d.CompletedLessons}/{practiceReq} clases formativas"
            + (categoryLabel is null ? "" : $" ({categoryLabel})")
            + $" · programa {programReq} h (la última es el examen)";
        var text = AssistantReplyFormatter.FormatPracticalDashboard(
            progressLine,
            $"Puede reservar clases: {YesNo(d.Eligibility.CanBookPractical)}. Puede agendar examen de manejo: {YesNo(d.CanBookPracticalExam)}",
            notes,
            reservations,
            lessons,
            exams,
            fullList);
        return (text, BuildPracticalUi(d, practiceReq, programReq, categoryLabel, fullList));
    }

    public static AssistantExamAvailabilityUi BuildPracticalUi(
        PracticalStudentDashboardDto d,
        int practiceReq,
        int programReq,
        string? categoryLabel,
        bool fullList)
    {
        var open = d.AvailableLessons
            .Where(l => string.Equals(l.BookingState, "can_reserve", StringComparison.OrdinalIgnoreCase)
                || string.Equals(l.BookingState, "reserved", StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.SessionDate)
            .ThenBy(l => l.StartTime)
            .ToList();
        var take = AssistantReplyFormatter.TakeCount(
            open.Count, fullList, AssistantReplyFormatter.PracticalPreview);
        var shown = open.Take(take).ToList();
        var canBookAny = shown.Any(l =>
            string.Equals(l.BookingState, "can_reserve", StringComparison.OrdinalIgnoreCase));

        var days = shown
            .GroupBy(l => l.SessionDate)
            .Select(g => new AssistantExamDayUi(
                g.Key,
                g.Select(l =>
                {
                    var isExam = string.Equals(l.Kind, "Exam", StringComparison.OrdinalIgnoreCase);
                    var canReserve = string.Equals(l.BookingState, "can_reserve", StringComparison.OrdinalIgnoreCase);
                    return new AssistantExamSlotUi(
                        Time: l.StartTime,
                        AvailableCapacity: Math.Max(0, l.AvailableSeats),
                        EndTime: l.EndTime,
                        Detail: $"{(isExam ? "Examen" : "Clase")} · {l.InstructorName} · {l.VehicleLabel}"
                            + (canReserve ? "" : " · ya reservada"),
                        SessionId: canReserve ? l.Id : null,
                        BookTool: canReserve
                            ? (isExam ? "reservar_examen_manejo" : "reservar_clase_manejo")
                            : null);
                }).ToList()))
            .Where(day => day.AvailableSlots.Count > 0)
            .ToList();

        var summary =
            $"Progreso: {d.CompletedLessons} de {practiceReq} clases formativas"
            + (categoryLabel is null ? "" : $" ({categoryLabel})")
            + $". Programa {programReq} h (incluye 1 h de examen).";

        return new AssistantExamAvailabilityUi(
            Type: "practical_slots",
            Title: "Cupos de clases de manejo",
            DateRangeFrom: days.FirstOrDefault()?.Date
                ?? ColombiaTime.TodayInColombia().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateRangeTo: days.LastOrDefault()?.Date
                ?? ColombiaTime.TodayInColombia().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CanBook: canBookAny && d.Eligibility.CanBookPractical,
            BlockReason: !d.Eligibility.CanBookPractical
                ? d.Eligibility.BlockReason
                : days.Count == 0
                    ? "No hay cupos de manejo publicados."
                    : null,
            MyBookingSummary: summary,
            Selectable: canBookAny && d.Eligibility.CanBookPractical,
            Days: days);
    }

    private async Task<string> SchoolSummaryAsync(AssistantUser user, CancellationToken ct)
    {
        var d = await _registry.GetDashboardAsync(SchoolOf(user), ct);
        return AssistantReplyFormatter.FormatSchoolSummary(
            d.ApprenticeCount,
            d.PendingEnrollmentCount,
            d.BalancePendingCount,
            d.BalancePendingTotal,
            d.ReadyForExamCount,
            d.NoExamAppointmentCount,
            d.ReadyForPracticalCount,
            d.ExamsNext7Days,
            d.TopReadyForExam.Select(x => (x.StudentName, x.StudentUserId)),
            d.TopNoExamAppointment.Select(x => (x.StudentName, x.StudentUserId)),
            d.TopBalanceDue.Select(x => (x.StudentName, x.BalanceDue)));
    }

    private async Task<string> StudentsProgressAsync(
        AssistantUser user, string? filter, string? search, bool fullList, CancellationToken ct)
    {
        var rows = await _theory.ListEnrollmentsAsync(SchoolOf(user), ct);
        IEnumerable<EnrollmentDto> query = rows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(x =>
                x.StudentName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.StudentEmail.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        query = (filter ?? "todos").Trim().ToLowerInvariant() switch
        {
            "listos_examen" => query.Where(x => x.PracticalEligibility is { } e
                && e.TheoryHoursComplete && e.WorkshopHoursComplete && !e.TheoryExamPassed && !x.TheoryExamAuthorized),
            "autorizados_examen" => query.Where(x => x.TheoryExamAuthorized && x.PracticalEligibility?.TheoryExamPassed != true),
            "listos_manejo" => query.Where(x => x.PracticalEligibility?.TheoryExamPassed == true && !x.PracticalAuthorized),
            "en_manejo" => query.Where(x => x.PracticalAuthorized),
            "con_saldo" => query.Where(x => x.BalanceDue > 0),
            "pendientes" => query.Where(x => x.Status == StudentEnrollmentStatuses.Pending),
            "faltan_horas" => query.Where(x => x.PracticalEligibility is not { TheoryHoursComplete: true, WorkshopHoursComplete: true }),
            _ => query
        };

        // A name search is already focused: show the full match set (still hard-capped).
        var effectiveFull = fullList || !string.IsNullOrWhiteSpace(search);
        return AssistantReplyFormatter.FormatStudentsProgress(query.ToList(), filter, effectiveFull);
    }

    private async Task<string> StudentDetailAsync(AssistantUser user, int studentUserId, CancellationToken ct)
    {
        var d = await _registry.GetDetailAsync(SchoolOf(user), studentUserId, ct);
        var p = d.Profile;
        var t = d.Training;
        return AssistantReplyFormatter.FormatStudentDetail(
            p.StudentName,
            p.StudentUserId,
            p.LicenseCategories ?? "sin asignar",
            DayType(p.AttendanceDayType),
            EnrollmentStatus(p.EnrollmentStatus),
            p.AmountPaid,
            p.AmountDue,
            p.BalanceDue,
            $"{t.TheoryHoursCompleted}/{t.TheoryHoursRequired} h",
            $"{t.WorkshopHoursCompleted}/{t.WorkshopHoursRequired} h",
            p.TheoryExamAuthorized,
            t.TheoryExamPassed,
            p.PracticalAuthorized,
            d.NextExam is { } exam ? $"{exam.ExamDate} a las {exam.SlotTime}" : null,
            $"{d.Practical.CompletedLessons}/{d.Practical.RequiredLessons} clases, {d.Practical.ScheduledLessons} programadas",
            d.Practical.NextLessonDate is not null
                ? $"{d.Practical.NextLessonDate} {d.Practical.NextLessonTime}"
                : null,
            !t.CanBookPractical ? t.BlockReason : null);
    }

    private async Task<string> SchoolAgendaAsync(
        AssistantUser user, DateOnly? from, DateOnly? to, bool fullList, CancellationToken ct)
    {
        var a = await _agenda.GetAsync(SchoolOf(user), from, to, ct);
        // Empty exam slots are noise here; cupos_examen_escuela lists free seats.
        var items = a.Items.Where(i => i.Kind != "exam" || i.Occupied > 0).ToList();
        return AssistantReplyFormatter.FormatAgenda(a, items, fullList);
    }

    private async Task<(string Text, AssistantExamAvailabilityUi Ui)> SchoolExamSlotsRichAsync(
        AssistantUser user, DateOnly? from, DateOnly? to, bool fullList, CancellationToken ct)
    {
        var w = await _exams.GetWeekAsync(SchoolOf(user), from, to, ct);
        var today = ColombiaTime.TodayInColombia().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var openDays = w.Days
            .Where(d => !d.IsClosed && string.CompareOrdinal(d.Date, today) >= 0)
            .ToList();
        var slotDays = openDays
            .Select(d => (d.Date, (IReadOnlyList<(string Time, int Available)>)d.Slots
                .Where(s => s.Available > 0)
                .Select(s => (s.Time, s.Available))
                .ToList()))
            .ToList();
        var block = w.HasTemplates ? null : "La escuela aún no tiene horarios de examen configurados.";
        var text = AssistantReplyFormatter.FormatExamSlotsText(
            w.From, w.To, canBook: false, block, null, slotDays, fullList);

        var days = openDays
            .Select(d => new AssistantExamDayUi(
                d.Date,
                d.Slots
                    .Where(s => s.Available > 0)
                    .Select(s => new AssistantExamSlotUi(s.Time, s.Available))
                    .ToList()))
            .Where(d => d.AvailableSlots.Count > 0)
            .ToList();

        var ui = new AssistantExamAvailabilityUi(
            Type: "exam_availability",
            Title: "Cupos del calendario de exámenes",
            DateRangeFrom: w.From,
            DateRangeTo: w.To,
            CanBook: false,
            BlockReason: block,
            MyBookingSummary: null,
            Selectable: false,
            Days: days);

        return (text, ui);
    }

    private async Task<string> PracticalOptionsAsync(AssistantUser user, bool fullList, CancellationToken ct)
    {
        var school = SchoolOf(user);
        var vehicles = await _practical.ListVehiclesAsync(school, true, ct);
        var instructors = (await _users.ListBySchoolAsync(school, ct))
            .Where(x => x.Role == Roles.Teacher)
            .OrderBy(x => x.Name)
            .ToList();
        var students = await _practical.ListSchedulingStudentsAsync(school, ct);
        return AssistantReplyFormatter.FormatPracticalOptions(
            vehicles.Select(v => (v.Id, v.Label + (v.Plate is null ? "" : " " + v.Plate))).ToList(),
            instructors.Select(i => (i.Id, i.Name)).ToList(),
            students.Select(s =>
            {
                var practiceReq = s.PracticeRequiredLessons > 0
                    ? s.PracticeRequiredLessons
                    : Math.Max(0, s.RequiredLessons - 1);
                return (
                    s.StudentUserId,
                    s.StudentName,
                    $"clases {s.CompletedLessons}/{practiceReq} (programa {s.RequiredLessons} h)",
                    s.IsEligible,
                    s.CanBookExam,
                    s.BlockReason);
            }).ToList(),
            fullList);
    }

    // ── Writes: description (before confirmation) ───────────────────────

    public async Task<string> DescribeAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct)
    {
        EnsureAllowed(user, tool, write: true);
        switch (tool)
        {
            case "reservar_clase_teorica":
            {
                var s = await TheorySessionAsync(user, ReqInt(args, "clase_id"), ct);
                return $"Reservar clase teórica: {s.Topic?.Name ?? "clase"}, {Day(s.SessionDate)} a las {Time(s.StartTime)}.";
            }
            case "cancelar_reserva_teorica":
            {
                var r = await TheoryReservationAsync(user, ReqInt(args, "reserva_id"), ct);
                return $"Cancelar tu clase teórica: {r.ClassSession?.Topic?.Name ?? "clase"}, {Day(r.ClassSession!.SessionDate)} a las {Time(r.ClassSession.StartTime)}.";
            }
            case "agendar_examen":
                return $"Agendar tu examen teórico: {Day(ReqDate(args, "fecha"))} a las {ReqTime(args, "hora")}.";
            case "cancelar_examen":
            {
                var a = await ExamAppointmentAsync(user, ReqInt(args, "cita_id"), ct);
                return $"Cancelar tu cita de examen teórico del {Day(a.ExamDate)} a las {Time(a.SlotTime)}.";
            }
            case "reservar_clase_manejo":
            {
                var l = await PracticalLessonAsync(user, ReqInt(args, "clase_id"), ct);
                if (PracticalSessionKinds.IsExam(l.Kind))
                {
                    throw new AssistantToolException(
                        "Ese cupo es un examen de manejo. Usa reservar_examen_manejo.");
                }

                return $"Reservar clase de manejo: {Day(l.SessionDate)} de {Time(l.StartTime)} a {Time(l.EndTime)}, vehículo {l.Vehicle?.Label ?? "—"}.";
            }
            case "reservar_examen_manejo":
            {
                var l = await PracticalLessonAsync(user, ReqInt(args, "clase_id"), ct);
                if (!PracticalSessionKinds.IsExam(l.Kind))
                {
                    throw new AssistantToolException(
                        "Ese cupo no es un examen de manejo. Usa reservar_clase_manejo o consulta clases_manejo.");
                }

                return $"Reservar examen de manejo: {Day(l.SessionDate)} de {Time(l.StartTime)} a {Time(l.EndTime)}, vehículo {l.Vehicle?.Label ?? "—"}.";
            }
            case "cancelar_reserva_manejo":
            {
                var r = await PracticalReservationAsync(user, ReqInt(args, "reserva_id"), ct);
                var kind = PracticalSessionKinds.IsExam(r.LessonSession?.Kind) ? "examen" : "clase";
                return $"Cancelar tu {kind} de manejo del {Day(r.LessonSession!.SessionDate)} a las {Time(r.LessonSession.StartTime)}.";
            }
            case "autorizar_examen":
            {
                var name = await SchoolStudentNameAsync(user, ReqInt(args, "estudiante_id"), ct);
                return ReqBool(args, "autorizar")
                    ? $"Habilitar el examen teórico de {name}."
                    : $"Quitar la autorización de examen teórico de {name}.";
            }
            case "autorizar_manejo":
            {
                var name = await SchoolStudentNameAsync(user, ReqInt(args, "estudiante_id"), ct);
                return ReqBool(args, "autorizar")
                    ? $"Habilitar las clases de manejo de {name}."
                    : $"Quitar la autorización de clases de manejo de {name}.";
            }
            case "agendar_examen_estudiante":
            {
                var name = await SchoolStudentNameAsync(user, ReqInt(args, "estudiante_id"), ct);
                return $"Agendar examen teórico de {name}: {Day(ReqDate(args, "fecha"))} a las {ReqTime(args, "hora")}.";
            }
            case "asignar_clase_manejo":
            {
                var school = SchoolOf(user);
                var name = await SchoolStudentNameAsync(user, ReqInt(args, "estudiante_id"), ct);
                var vehicleId = ReqInt(args, "vehiculo_id");
                var vehicle = await _db.Set<PracticalVehicle>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == vehicleId && x.SchoolUserId == school, ct)
                    ?? throw new AssistantToolException("Ese vehículo no existe en la escuela.");
                var instructorName = await SchoolStaffNameAsync(user, ReqInt(args, "instructor_id"), ct);
                return $"Asignar clase de manejo a {name}: {Day(ReqDate(args, "fecha"))} de {ReqTime(args, "hora_inicio")} a {ReqTime(args, "hora_fin")}, con {instructorName} en {vehicle.Label}.";
            }
            case "asignar_examen_manejo":
            {
                var school = SchoolOf(user);
                var name = await SchoolStudentNameAsync(user, ReqInt(args, "estudiante_id"), ct);
                var vehicleId = ReqInt(args, "vehiculo_id");
                var vehicle = await _db.Set<PracticalVehicle>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == vehicleId && x.SchoolUserId == school, ct)
                    ?? throw new AssistantToolException("Ese vehículo no existe en la escuela.");
                var instructorName = await SchoolStaffNameAsync(user, ReqInt(args, "instructor_id"), ct);
                var start = ReqTime(args, "hora_inicio");
                return $"Asignar examen de manejo (1 h) a {name}: {Day(ReqDate(args, "fecha"))} desde {start}, con {instructorName} en {vehicle.Label}.";
            }
            default:
                throw new AssistantToolException($"Herramienta desconocida: {tool}.");
        }
    }

    // ── Writes: execution (after confirmation) ──────────────────────────

    public async Task<string> ExecuteAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct)
    {
        EnsureAllowed(user, tool, write: true);
        switch (tool)
        {
            case "reservar_clase_teorica":
            {
                var s = await _theory.ReserveAsync(user.UserId, ReqInt(args, "clase_id"), ct);
                return $"Listo. Quedó reservada tu clase de {s.TopicName} el {Day(s.SessionDate)} a las {s.StartTime}.";
            }
            case "cancelar_reserva_teorica":
                await _theory.CancelReservationAsync(user.UserId, ReqInt(args, "reserva_id"), ct);
                return "Listo. Cancelamos tu reserva de clase teórica.";
            case "agendar_examen":
            {
                var b = await _exams.BookAsStudentAsync(
                    user.UserId,
                    new StudentBookExamRequest(ReqDate(args, "fecha"), ReqTime(args, "hora")),
                    ct);
                return $"Listo. Tu examen teórico quedó agendado el {DayIso(b.Date)} a las {b.Time}.";
            }
            case "cancelar_examen":
                await _exams.CancelAsStudentAsync(user.UserId, ReqInt(args, "cita_id"), ct);
                return "Listo. Cancelamos tu cita de examen teórico.";
            case "reservar_clase_manejo":
            {
                var lessonId = ReqInt(args, "clase_id");
                var existing = await PracticalLessonAsync(user, lessonId, ct);
                if (PracticalSessionKinds.IsExam(existing.Kind))
                {
                    throw new AssistantToolException(
                        "Ese cupo es un examen de manejo. Usa reservar_examen_manejo.");
                }

                var l = await _practical.ReserveAsync(user.UserId, lessonId, ct);
                return $"Listo. Quedó reservada tu clase de manejo el {DayIso(l.SessionDate)} a las {l.StartTime} con {l.InstructorName}.";
            }
            case "reservar_examen_manejo":
            {
                var lessonId = ReqInt(args, "clase_id");
                var existing = await PracticalLessonAsync(user, lessonId, ct);
                if (!PracticalSessionKinds.IsExam(existing.Kind))
                {
                    throw new AssistantToolException(
                        "Ese cupo no es un examen de manejo. Usa reservar_clase_manejo.");
                }

                var l = await _practical.ReserveAsync(user.UserId, lessonId, ct);
                return $"Listo. Quedó agendado tu examen de manejo el {DayIso(l.SessionDate)} a las {l.StartTime} con {l.InstructorName}.";
            }
            case "cancelar_reserva_manejo":
                await _practical.CancelReservationAsync(user.UserId, ReqInt(args, "reserva_id"), ct);
                return "Listo. Cancelamos tu reserva de manejo.";
            case "autorizar_examen":
            case "autorizar_manejo":
            {
                var school = SchoolOf(user);
                var studentId = ReqInt(args, "estudiante_id");
                var name = await SchoolStudentNameAsync(user, studentId, ct);
                var authorize = ReqBool(args, "autorizar");
                var status = await _db.Set<SchoolStudentEnrollment>().AsNoTracking()
                    .Where(x => x.SchoolUserId == school && x.StudentUserId == studentId)
                    .Select(x => x.Status)
                    .FirstOrDefaultAsync(ct) ?? StudentEnrollmentStatuses.Pending;
                var request = tool == "autorizar_examen"
                    ? new UpdateEnrollmentRequest(status, TheoryExamAuthorized: authorize)
                    : new UpdateEnrollmentRequest(status, PracticalAuthorized: authorize);
                await _theory.UpdateEnrollmentAsync(school, studentId, request, user.UserId, ct);
                return (tool, authorize) switch
                {
                    ("autorizar_examen", true) => $"Listo. {name} ya puede agendar su examen teórico y le llegó el aviso.",
                    ("autorizar_examen", false) => $"Listo. Quitamos la autorización del examen teórico a {name}.",
                    (_, true) => $"Listo. {name} ya puede reservar clases de manejo y le llegó el aviso.",
                    _ => $"Listo. Quitamos la autorización de clases de manejo a {name}."
                };
            }
            case "agendar_examen_estudiante":
            {
                var school = SchoolOf(user);
                var studentId = ReqInt(args, "estudiante_id");
                var name = await SchoolStudentNameAsync(user, studentId, ct);
                var date = ReqDate(args, "fecha");
                var time = ReqTime(args, "hora");
                await _exams.BookAsSchoolAsync(
                    school,
                    user.UserId,
                    new SchoolBookExamRequest(date, time, studentId, null, "Agendado con el asistente"),
                    ct);
                return $"Listo. Examen teórico de {name} agendado el {Day(date)} a las {time}.";
            }
            case "asignar_clase_manejo":
            {
                var school = SchoolOf(user);
                var studentId = ReqInt(args, "estudiante_id");
                var name = await SchoolStudentNameAsync(user, studentId, ct);
                var date = ReqDate(args, "fecha");
                var lesson = await _practical.QuickAssignAsync(
                    school,
                    new QuickAssignPracticalRequest(
                        date,
                        ReqTime(args, "hora_inicio"),
                        ReqTime(args, "hora_fin"),
                        ReqInt(args, "instructor_id"),
                        ReqInt(args, "vehiculo_id"),
                        studentId,
                        PracticalSessionKinds.Lesson),
                    ct);
                return $"Listo. Clase de manejo de {name} asignada el {Day(date)} de {lesson.StartTime} a {lesson.EndTime}.";
            }
            case "asignar_examen_manejo":
            {
                var school = SchoolOf(user);
                var studentId = ReqInt(args, "estudiante_id");
                var name = await SchoolStudentNameAsync(user, studentId, ct);
                var date = ReqDate(args, "fecha");
                var start = ReqTime(args, "hora_inicio");
                if (!TimeOnly.TryParse(start, out var startTime))
                {
                    throw new AssistantToolException("hora_inicio inválida. Usa HH:mm.");
                }

                var endTime = startTime.Add(TimeSpan.FromHours(1)).AddMinutes(-1);
                var lesson = await _practical.QuickAssignAsync(
                    school,
                    new QuickAssignPracticalRequest(
                        date,
                        startTime.ToString("HH:mm"),
                        endTime.ToString("HH:mm"),
                        ReqInt(args, "instructor_id"),
                        ReqInt(args, "vehiculo_id"),
                        studentId,
                        PracticalSessionKinds.Exam),
                    ct);
                return $"Listo. Examen de manejo de {name} asignado el {Day(date)} de {lesson.StartTime} a {lesson.EndTime}.";
            }
            default:
                throw new AssistantToolException($"Herramienta desconocida: {tool}.");
        }
    }

    // ── Lookups scoped to the current user ──────────────────────────────

    private async Task<TheoryClassSession> TheorySessionAsync(AssistantUser user, int id, CancellationToken ct) =>
        await _db.Set<TheoryClassSession>().AsNoTracking().Include(x => x.Topic)
            .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == user.SchoolUserId, ct)
        ?? throw new AssistantToolException("No encontré esa clase teórica. Consulta primero las clases disponibles.");

    private async Task<TheoryClassReservation> TheoryReservationAsync(AssistantUser user, int id, CancellationToken ct) =>
        await _db.Set<TheoryClassReservation>().AsNoTracking()
            .Include(x => x.ClassSession).ThenInclude(x => x!.Topic)
            .FirstOrDefaultAsync(x => x.Id == id && x.StudentUserId == user.UserId, ct)
        ?? throw new AssistantToolException("No encontré esa reserva teórica del estudiante.");

    private async Task<TheoryExamAppointment> ExamAppointmentAsync(AssistantUser user, int id, CancellationToken ct) =>
        await _db.Set<TheoryExamAppointment>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.StudentUserId == user.UserId, ct)
        ?? throw new AssistantToolException("No encontré esa cita de examen del estudiante.");

    private async Task<PracticalLessonSession> PracticalLessonAsync(AssistantUser user, int id, CancellationToken ct) =>
        await _db.Set<PracticalLessonSession>().AsNoTracking().Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == user.SchoolUserId, ct)
        ?? throw new AssistantToolException("No encontré esa clase de manejo. Consulta primero las clases disponibles.");

    private async Task<PracticalLessonReservation> PracticalReservationAsync(AssistantUser user, int id, CancellationToken ct) =>
        await _db.Set<PracticalLessonReservation>().AsNoTracking().Include(x => x.LessonSession)
            .FirstOrDefaultAsync(x => x.Id == id && x.StudentUserId == user.UserId, ct)
        ?? throw new AssistantToolException("No encontré esa reserva de manejo del estudiante.");

    private async Task<string> SchoolStudentNameAsync(AssistantUser user, int studentUserId, CancellationToken ct)
    {
        var student = await _users.GetByIdAsync(studentUserId, ct);
        if (student is null || student.Role != Roles.Student || student.SchoolId != SchoolOf(user))
        {
            throw new AssistantToolException("Ese estudiante no pertenece a la escuela. Busca primero su estudiante_id.");
        }

        return student.Name;
    }

    private async Task<string> SchoolStaffNameAsync(AssistantUser user, int instructorUserId, CancellationToken ct)
    {
        var instructor = await _users.GetByIdAsync(instructorUserId, ct);
        if (instructor is null || instructor.Role != Roles.Teacher || instructor.SchoolId != SchoolOf(user))
        {
            throw new AssistantToolException("Ese instructor no pertenece a la escuela. Consulta opciones_manejo.");
        }

        return instructor.Name;
    }

    private static int SchoolOf(AssistantUser user) =>
        user.Role == Roles.School ? user.UserId : throw new AssistantToolException("Solo disponible para escuelas.");

    private void EnsureAllowed(AssistantUser user, string tool, bool write)
    {
        var def = ToolsFor(user.Role).FirstOrDefault(x => x.Name == tool)
            ?? throw new AssistantToolException($"La herramienta {tool} no está disponible para este usuario.");
        if (def.RequiresConfirmation != write)
        {
            throw new AssistantToolException($"Uso no válido de {tool}.");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static AssistantToolDefinition Read(string name, string description, params JsonObject[] props) =>
        new(name, description, Schema(props), false);

    private static AssistantToolDefinition Write(string name, string description, params JsonObject[] props) =>
        new(name, description, Schema(props), true);

    private static JsonObject P(string name, string type, string description, bool required = false) =>
        new() { ["name"] = name, ["type"] = type, ["description"] = description, ["required"] = required };

    private static JsonObject Schema(JsonObject[] props)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var p in props)
        {
            var name = p["name"]!.GetValue<string>();
            properties[name] = new JsonObject
            {
                ["type"] = p["type"]!.GetValue<string>(),
                ["description"] = p["description"]!.GetValue<string>()
            };
            if (p["required"]!.GetValue<bool>())
            {
                required.Add(name);
            }
        }

        return new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = required };
    }

    private static bool WantsFullList(JsonObject args)
    {
        if (args["completo"] is not JsonValue v)
        {
            return false;
        }

        if (v.TryGetValue<bool>(out var b))
        {
            return b;
        }

        if (v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
        {
            var t = s.Trim().ToLowerInvariant();
            return t is "true" or "1" or "si" or "sí" or "yes" or "todos" or "completo";
        }

        return false;
    }

    private static string? Str(JsonObject args, string key) =>
        args[key] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    private static int ReqInt(JsonObject args, string key)
    {
        if (args[key] is JsonValue v)
        {
            if (v.TryGetValue<int>(out var i) && i > 0) return i;
            if (v.TryGetValue<double>(out var d) && d > 0 && d == Math.Floor(d)) return (int)d;
            if (v.TryGetValue<string>(out var s) && int.TryParse(s, out var p) && p > 0) return p;
        }

        throw new AssistantToolException($"Falta el dato {key} (número).");
    }

    private static bool ReqBool(JsonObject args, string key)
    {
        if (args[key] is JsonValue v)
        {
            if (v.TryGetValue<bool>(out var b)) return b;
            if (v.TryGetValue<string>(out var s) && bool.TryParse(s, out var p)) return p;
        }

        throw new AssistantToolException($"Falta el dato {key} (true o false).");
    }

    private static DateOnly? OptDate(JsonObject args, string key) =>
        Str(args, key) is { } s ? ParseDate(s, key) : null;

    private static DateOnly ReqDate(JsonObject args, string key) =>
        Str(args, key) is { } s ? ParseDate(s, key) : throw new AssistantToolException($"Falta la fecha {key} (AAAA-MM-DD).");

    private static DateOnly ParseDate(string s, string key) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new AssistantToolException($"La fecha {key} debe tener formato AAAA-MM-DD.");

    private static string ReqTime(JsonObject args, string key)
    {
        var s = Str(args, key) ?? throw new AssistantToolException($"Falta la hora {key} (HH:mm).");
        return TimeOnly.TryParse(s, CultureInfo.InvariantCulture, out var t)
            ? t.ToString("HH:mm", CultureInfo.InvariantCulture)
            : throw new AssistantToolException($"La hora {key} debe tener formato HH:mm.");
    }

    private static readonly string[] DayNames = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private static string Day(DateOnly d) => $"{DayNames[(int)d.DayOfWeek]} {d:yyyy-MM-dd}";

    private static string DayIso(string iso) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? Day(d) : iso;

    private static string Time(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string YesNo(bool value) => value ? "sí" : "no";

    private static string AuthorizedLabel(bool value) => value ? "autorizado" : "no autorizado";

    private static string Hours(decimal value) =>
        value == decimal.Truncate(value)
            ? ((int)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Money(decimal value) =>
        "$" + value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');

    private static string DayType(string? type) => type switch
    {
        "Weekday" => "semana (lunes a viernes)",
        "Saturday" => "sábados",
        _ => "sin asignar"
    };

    private static string EnrollmentStatus(string status) => status switch
    {
        "Active" or "Accepted" => "autorizada",
        "Suspended" => "suspendida",
        _ => "pendiente"
    };

    private static string Status(string status) => status switch
    {
        "Scheduled" => "programada",
        "Cancelled" => "cancelada",
        "Completed" => "realizada",
        "Past" => "ya pasó",
        "Available" => "con cupo",
        "Full" => "lleno",
        "Closed" => "cerrado",
        _ => status
    };

}
