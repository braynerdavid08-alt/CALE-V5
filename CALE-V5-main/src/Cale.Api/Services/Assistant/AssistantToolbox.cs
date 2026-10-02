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
    private const int MaxLines = 60;

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
            P("semana_inicio", "string", "Lunes de la semana en formato AAAA-MM-DD. Vacío = semana actual.")),
        Write("reservar_clase_teorica", "Prepara la reserva de una clase teórica (el usuario debe confirmar).",
            P("clase_id", "integer", "clase_id de la lista de clases teóricas.", true)),
        Write("cancelar_reserva_teorica", "Prepara la cancelación de una reserva de clase teórica.",
            P("reserva_id", "integer", "reserva_id de la clase reservada.", true)),
        Read("cupos_examen", "Cupos disponibles para el examen teórico y la cita actual del estudiante.",
            P("desde", "string", "Fecha inicial AAAA-MM-DD (opcional)."),
            P("hasta", "string", "Fecha final AAAA-MM-DD (opcional).")),
        Write("agendar_examen", "Prepara la cita del examen teórico en una fecha y hora con cupo.",
            P("fecha", "string", "Fecha AAAA-MM-DD.", true),
            P("hora", "string", "Hora HH:mm tal como aparece en los cupos.", true)),
        Write("cancelar_examen", "Prepara la cancelación de la cita de examen teórico.",
            P("cita_id", "integer", "cita_id de la cita actual.", true)),
        Read("clases_manejo", "Clases de manejo: si puede reservar, clases disponibles y reservas actuales."),
        Write("reservar_clase_manejo", "Prepara la reserva de una clase de manejo disponible.",
            P("clase_id", "integer", "clase_id de la lista de clases de manejo.", true)),
        Write("cancelar_reserva_manejo", "Prepara la cancelación de una clase de manejo reservada.",
            P("reserva_id", "integer", "reserva_id de la clase de manejo.", true))
    ];

    private static readonly AssistantToolDefinition[] SchoolTools =
    [
        Read("resumen_escuela", "Resumen general: estudiantes, saldos, listos para examen o manejo, exámenes próximos."),
        Read("progreso_estudiantes", "Progreso de los estudiantes (horas, examen, manejo, saldo). Se puede filtrar o buscar por nombre.",
            P("filtro", "string", "todos | listos_examen | autorizados_examen | listos_manejo | en_manejo | con_saldo | pendientes | faltan_horas"),
            P("buscar", "string", "Parte del nombre o correo del estudiante.")),
        Read("detalle_estudiante", "Expediente de un estudiante: datos, horas, examen, práctica, saldo y autorizaciones.",
            P("estudiante_id", "integer", "estudiante_id de la lista de progreso.", true)),
        Read("agenda_escuela", "Agenda de clases teóricas, exámenes y clases de manejo en un rango de fechas.",
            P("desde", "string", "Fecha inicial AAAA-MM-DD (opcional, por defecto esta semana)."),
            P("hasta", "string", "Fecha final AAAA-MM-DD (opcional).")),
        Read("cupos_examen_escuela", "Cupos libres del calendario de exámenes teóricos.",
            P("desde", "string", "Fecha inicial AAAA-MM-DD (opcional)."),
            P("hasta", "string", "Fecha final AAAA-MM-DD (opcional).")),
        Read("opciones_manejo", "Vehículos, instructores y estudiantes habilitados para asignar clases de manejo."),
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
            P("vehiculo_id", "integer", "vehiculo_id de opciones_manejo.", true))
    ];

    public IReadOnlyList<AssistantToolDefinition> ToolsFor(string role) => role switch
    {
        Roles.Student => StudentTools,
        Roles.School => SchoolTools,
        _ => []
    };

    // ── Reads ───────────────────────────────────────────────────────────

    public Task<string> ReadAsync(AssistantUser user, string tool, JsonObject args, CancellationToken ct)
    {
        EnsureAllowed(user, tool, write: false);
        return tool switch
        {
            "mi_progreso" => MyProgressAsync(user, ct),
            "clases_teoricas" => TheoryWeekAsync(user, OptDate(args, "semana_inicio"), ct),
            "cupos_examen" => StudentExamSlotsAsync(user, OptDate(args, "desde"), OptDate(args, "hasta"), ct),
            "clases_manejo" => StudentPracticalAsync(user, ct),
            "resumen_escuela" => SchoolSummaryAsync(user, ct),
            "progreso_estudiantes" => StudentsProgressAsync(user, Str(args, "filtro"), Str(args, "buscar"), ct),
            "detalle_estudiante" => StudentDetailAsync(user, ReqInt(args, "estudiante_id"), ct),
            "agenda_escuela" => SchoolAgendaAsync(user, OptDate(args, "desde"), OptDate(args, "hasta"), ct),
            "cupos_examen_escuela" => SchoolExamSlotsAsync(user, OptDate(args, "desde"), OptDate(args, "hasta"), ct),
            "opciones_manejo" => PracticalOptionsAsync(user, ct),
            _ => throw new AssistantToolException($"Herramienta desconocida: {tool}.")
        };
    }

    private async Task<string> MyProgressAsync(AssistantUser user, CancellationToken ct)
    {
        var d = await _theory.GetStudentDashboardAsync(user.UserId, ct);
        var sb = new StringBuilder();
        sb.AppendLine($"Horas de teoría: {d.HoursCompleted}/{d.HoursRequired}. Taller: {d.WorkshopHoursCompleted}/{d.WorkshopHoursRequired}. Faltas: {d.Absences}.");
        sb.AppendLine($"Grupo: {DayType(d.AttendanceDayType)}. Saldo pendiente: {Money(d.BalanceDue)}.");
        if (d.NextClass is { } next)
        {
            sb.AppendLine($"Próxima clase teórica: {next.TopicName}, {Day(next.SessionDate)} {next.StartTime}.");
        }

        foreach (var r in d.UpcomingReservations.Take(10))
        {
            sb.AppendLine($"Reserva teórica reserva_id={r.MyReservationId}: {r.TopicName}, {Day(r.SessionDate)} {r.StartTime}.");
        }

        var e = d.PracticalEligibility;
        if (e is not null)
        {
            sb.AppendLine($"Examen teórico: autorizado={YesNo(e.TheoryExamAuthorized)}, aprobado={YesNo(e.TheoryExamPassed)}.");
            sb.AppendLine($"Clases de manejo: autorizado={YesNo(e.PracticalAuthorized)}, puede reservar={YesNo(e.CanBookPractical)}.");
            if (!string.IsNullOrWhiteSpace(e.BlockReason))
            {
                sb.AppendLine($"Lo que falta: {e.BlockReason}");
            }
        }

        sb.AppendLine(d.NextExamAppointment is { } exam
            ? $"Cita de examen cita_id={exam.Id}: {exam.ExamDate} {exam.SlotTime}."
            : "Sin cita de examen.");
        if (!string.IsNullOrWhiteSpace(d.NextAction))
        {
            sb.AppendLine($"Sugerencia de la app: {d.NextAction}");
        }

        return sb.ToString();
    }

    private async Task<string> TheoryWeekAsync(AssistantUser user, DateOnly? weekStart, CancellationToken ct)
    {
        var w = await _theory.GetStudentWeekScheduleAsync(user.UserId, weekStart, ct);
        var sb = new StringBuilder();
        sb.AppendLine($"Semana {w.WeekStart:yyyy-MM-dd} a {w.WeekEnd:yyyy-MM-dd}. Grupo del estudiante: {DayType(w.StudentAttendanceDayType)}.");
        var sessions = w.Sessions.OrderBy(x => x.SessionDate).ThenBy(x => x.StartTime).ToList();
        if (sessions.Count == 0)
        {
            sb.AppendLine("No hay clases programadas esa semana.");
        }

        foreach (var s in sessions.Take(MaxLines))
        {
            var mine = s.MyReservationId is int rid ? $" | YA RESERVADA reserva_id={rid}" : "";
            var state = string.IsNullOrWhiteSpace(s.BookingMessage) ? s.BookingState : $"{s.BookingState}: {s.BookingMessage}";
            sb.AppendLine($"clase_id={s.Id} | {Day(s.SessionDate)} {s.StartTime}-{s.EndTime} | {s.TopicName} | {s.ClassroomName} | cupos {s.AvailableSeats}/{s.Capacity} | {Status(s.Status)} | {state}{mine}");
        }

        return sb.ToString();
    }

    private async Task<string> StudentExamSlotsAsync(AssistantUser user, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var a = await _exams.GetStudentAvailabilityAsync(user.UserId, from, to, ct);
        var sb = new StringBuilder();
        sb.AppendLine($"Rango {a.From} a {a.To}. Puede agendar: {YesNo(a.CanBook)}.");
        if (!string.IsNullOrWhiteSpace(a.BlockReason))
        {
            sb.AppendLine($"Motivo: {a.BlockReason}");
        }

        if (a.MyBooking is { } b)
        {
            sb.AppendLine($"Cita actual cita_id={b.Id}: {b.Date} {b.Time} ({b.Status}). Puede cancelar: {YesNo(b.CanCancel)} (mínimo {a.MinCancelHours} h antes).");
        }

        AppendSlots(sb, a.Days.Select(d => (d.Date, d.Slots.Where(s => s.Available > 0).Select(s => (s.Time, s.Available)))));
        return sb.ToString();
    }

    private async Task<string> StudentPracticalAsync(AssistantUser user, CancellationToken ct)
    {
        var d = await _practical.GetStudentDashboardAsync(user.UserId, ct);
        var sb = new StringBuilder();
        sb.AppendLine($"Puede reservar clases de manejo: {YesNo(d.Eligibility.CanBookPractical)}. Clases completadas: {d.CompletedLessons}/{d.RequiredLessons}.");
        if (!d.Eligibility.CanBookPractical && !string.IsNullOrWhiteSpace(d.Eligibility.BlockReason))
        {
            sb.AppendLine($"Motivo: {d.Eligibility.BlockReason}");
        }

        foreach (var r in d.UpcomingReservations.Take(10))
        {
            sb.AppendLine($"Reservada reserva_id={r.MyReservationId}: {r.SessionDate} {r.StartTime}-{r.EndTime}, instructor {r.InstructorName}, vehículo {r.VehicleLabel}.");
        }

        if (d.AvailableLessons.Count == 0)
        {
            sb.AppendLine("No hay clases de manejo disponibles para reservar.");
        }

        foreach (var l in d.AvailableLessons.Take(30))
        {
            sb.AppendLine($"clase_id={l.Id} | {DayIso(l.SessionDate)} {l.StartTime}-{l.EndTime} | instructor {l.InstructorName} | vehículo {l.VehicleLabel} | cupos {l.AvailableSeats}");
        }

        return sb.ToString();
    }

    private async Task<string> SchoolSummaryAsync(AssistantUser user, CancellationToken ct)
    {
        var d = await _registry.GetDashboardAsync(SchoolOf(user), ct);
        var sb = new StringBuilder();
        sb.AppendLine($"Estudiantes: {d.ApprenticeCount}. Matrículas pendientes: {d.PendingEnrollmentCount}.");
        sb.AppendLine($"Con saldo: {d.BalancePendingCount} (total {Money(d.BalancePendingTotal)}).");
        sb.AppendLine($"Listos para examen: {d.ReadyForExamCount}. Sin cita de examen: {d.NoExamAppointmentCount}. Listos para manejo: {d.ReadyForPracticalCount}.");
        sb.AppendLine($"Exámenes en los próximos 7 días: {d.ExamsNext7Days}.");
        if (d.TopReadyForExam.Count > 0)
        {
            sb.AppendLine("Listos para examen: " + string.Join(", ", d.TopReadyForExam.Select(x => $"{x.StudentName} (estudiante_id={x.StudentUserId})")));
        }

        if (d.TopNoExamAppointment.Count > 0)
        {
            sb.AppendLine("Sin cita de examen: " + string.Join(", ", d.TopNoExamAppointment.Select(x => $"{x.StudentName} (estudiante_id={x.StudentUserId})")));
        }

        if (d.TopBalanceDue.Count > 0)
        {
            sb.AppendLine("Mayores saldos: " + string.Join(", ", d.TopBalanceDue.Select(x => $"{x.StudentName} {Money(x.BalanceDue)}")));
        }

        return sb.ToString();
    }

    private async Task<string> StudentsProgressAsync(AssistantUser user, string? filter, string? search, CancellationToken ct)
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

        var list = query.ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"{list.Count} estudiante(s).");
        foreach (var x in list.Take(MaxLines))
        {
            var e = x.PracticalEligibility;
            var hours = e is null ? "horas ?" : $"teoría {e.TheoryHoursCompleted}/{e.TheoryHoursRequired}, taller {e.WorkshopHoursCompleted}/{e.WorkshopHoursRequired}";
            var exam = e?.TheoryExamPassed == true ? "aprobado" : x.TheoryExamAuthorized ? "autorizado" : "no autorizado";
            sb.AppendLine($"estudiante_id={x.StudentUserId} | {x.StudentName} | matrícula {EnrollmentStatus(x.Status)} | {hours} | examen {exam} | manejo {(x.PracticalAuthorized ? "autorizado" : "no")} | saldo {Money(x.BalanceDue)}");
        }

        if (list.Count > MaxLines)
        {
            sb.AppendLine($"(Se muestran {MaxLines}; usa filtro o buscar para ver el resto.)");
        }

        return sb.ToString();
    }

    private async Task<string> StudentDetailAsync(AssistantUser user, int studentUserId, CancellationToken ct)
    {
        var d = await _registry.GetDetailAsync(SchoolOf(user), studentUserId, ct);
        var p = d.Profile;
        var t = d.Training;
        var sb = new StringBuilder();
        sb.AppendLine($"{p.StudentName} (estudiante_id={p.StudentUserId}). Documento {p.DocumentNumber ?? "—"}, celular {p.Phone ?? "—"}, categoría {p.LicenseCategories ?? "sin asignar"}, grupo {DayType(p.AttendanceDayType)}, matrícula {EnrollmentStatus(p.EnrollmentStatus)}.");
        sb.AppendLine($"Pagado {Money(p.AmountPaid)} de {Money(p.AmountDue)}. Saldo {Money(p.BalanceDue)}.");
        sb.AppendLine($"Teoría {t.TheoryHoursCompleted}/{t.TheoryHoursRequired} h, taller {t.WorkshopHoursCompleted}/{t.WorkshopHoursRequired} h.");
        sb.AppendLine($"Examen: autorizado={YesNo(p.TheoryExamAuthorized)}, aprobado={YesNo(t.TheoryExamPassed)}. Manejo autorizado={YesNo(p.PracticalAuthorized)}.");
        sb.AppendLine(d.NextExam is { } exam ? $"Cita de examen: {exam.ExamDate} {exam.SlotTime}." : "Sin cita de examen.");
        sb.AppendLine($"Práctica: {d.Practical.CompletedLessons}/{d.Practical.RequiredLessons} clases, {d.Practical.ScheduledLessons} programadas.");
        if (d.Practical.NextLessonDate is not null)
        {
            sb.AppendLine($"Próxima clase de manejo: {d.Practical.NextLessonDate} {d.Practical.NextLessonTime}.");
        }

        if (!t.CanBookPractical && !string.IsNullOrWhiteSpace(t.BlockReason))
        {
            sb.AppendLine($"Lo que falta: {t.BlockReason}");
        }

        return sb.ToString();
    }

    private async Task<string> SchoolAgendaAsync(AssistantUser user, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var a = await _agenda.GetAsync(SchoolOf(user), from, to, ct);
        // Empty exam slots are noise here; cupos_examen_escuela lists free seats.
        var items = a.Items.Where(i => i.Kind != "exam" || i.Occupied > 0).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"Agenda {a.From} a {a.To}: {items.Count} actividad(es).");
        foreach (var i in items.Take(MaxLines))
        {
            var names = i.Students.Count == 0
                ? ""
                : " | " + string.Join(", ", i.Students.Take(8).Select(s => s.Name)) + (i.Students.Count > 8 ? "…" : "");
            sb.AppendLine($"{AgendaKind(i.Kind)} | {DayIso(i.Date)} {i.StartTime}{(i.EndTime is null ? "" : "-" + i.EndTime)} | {i.Title} | {i.InstructorName ?? "sin instructor"} | {i.Occupied}/{i.Capacity} ocupados | {Status(i.Status)}{names}");
        }

        return sb.ToString();
    }

    private async Task<string> SchoolExamSlotsAsync(AssistantUser user, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var w = await _exams.GetWeekAsync(SchoolOf(user), from, to, ct);
        var sb = new StringBuilder();
        sb.AppendLine($"Calendario de exámenes {w.From} a {w.To}.");
        if (!w.HasTemplates)
        {
            sb.AppendLine("La escuela aún no tiene horarios de examen configurados.");
        }

        var today = ColombiaTime.TodayInColombia().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        AppendSlots(sb, w.Days
            .Where(d => !d.IsClosed && string.CompareOrdinal(d.Date, today) >= 0)
            .Select(d => (d.Date, d.Slots.Where(s => s.Available > 0).Select(s => (s.Time, s.Available)))));
        return sb.ToString();
    }

    private async Task<string> PracticalOptionsAsync(AssistantUser user, CancellationToken ct)
    {
        var school = SchoolOf(user);
        var vehicles = await _practical.ListVehiclesAsync(school, true, ct);
        var instructors = (await _users.ListBySchoolAsync(school, ct))
            .Where(x => x.Role == Roles.Teacher)
            .OrderBy(x => x.Name)
            .ToList();
        var students = await _practical.ListSchedulingStudentsAsync(school, ct);
        var sb = new StringBuilder();
        sb.AppendLine("Vehículos: " + (vehicles.Count == 0 ? "ninguno activo" : string.Join(", ", vehicles.Select(v => $"{v.Label}{(v.Plate is null ? "" : " " + v.Plate)} (vehiculo_id={v.Id})"))));
        sb.AppendLine("Instructores: " + (instructors.Count == 0 ? "ninguno" : string.Join(", ", instructors.Select(i => $"{i.Name} (instructor_id={i.Id})"))));
        sb.AppendLine("Estudiantes autorizados para manejo:");
        foreach (var s in students.Take(MaxLines))
        {
            sb.AppendLine($"estudiante_id={s.StudentUserId} | {s.StudentName} | clases {s.CompletedLessons}/{s.RequiredLessons} | puede reservar {YesNo(s.IsEligible)}{(s.BlockReason is null ? "" : " | " + s.BlockReason)}");
        }

        return sb.ToString();
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
                return $"Reservar clase de manejo: {Day(l.SessionDate)} de {Time(l.StartTime)} a {Time(l.EndTime)}, vehículo {l.Vehicle?.Label ?? "—"}.";
            }
            case "cancelar_reserva_manejo":
            {
                var r = await PracticalReservationAsync(user, ReqInt(args, "reserva_id"), ct);
                return $"Cancelar tu clase de manejo del {Day(r.LessonSession!.SessionDate)} a las {Time(r.LessonSession.StartTime)}.";
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
                var l = await _practical.ReserveAsync(user.UserId, ReqInt(args, "clase_id"), ct);
                return $"Listo. Quedó reservada tu clase de manejo el {DayIso(l.SessionDate)} a las {l.StartTime} con {l.InstructorName}.";
            }
            case "cancelar_reserva_manejo":
                await _practical.CancelReservationAsync(user.UserId, ReqInt(args, "reserva_id"), ct);
                return "Listo. Cancelamos tu clase de manejo.";
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
                        studentId),
                    ct);
                return $"Listo. Clase de manejo de {name} asignada el {Day(date)} de {lesson.StartTime} a {lesson.EndTime}.";
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

    private static void AppendSlots(StringBuilder sb, IEnumerable<(string Date, IEnumerable<(string Time, int Available)> Slots)> days)
    {
        var any = false;
        foreach (var (date, slots) in days)
        {
            var list = slots.ToList();
            if (list.Count == 0)
            {
                continue;
            }

            any = true;
            sb.AppendLine($"{DayIso(date)}: " + string.Join(", ", list.Select(s => $"{s.Time} ({s.Available} cupo{(s.Available == 1 ? "" : "s")})")));
        }

        if (!any)
        {
            sb.AppendLine("No hay cupos libres en ese rango.");
        }
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

    private static string AgendaKind(string kind) => kind.ToLowerInvariant() switch
    {
        "theory" => "Clase teórica",
        "exam" => "Examen",
        "practical" => "Clase de manejo",
        _ => kind
    };
}
