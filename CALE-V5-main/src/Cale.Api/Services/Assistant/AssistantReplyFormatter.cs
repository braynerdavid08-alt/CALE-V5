using System.Globalization;
using System.Text;
using Cale.Modules.TheoreticalTraining.Application.DTOs;

namespace Cale.Api.Services.Assistant;

/// <summary>
/// Turns tool query results into short, scannable Spanish replies for people
/// (and keeps optional *_id tags the model needs for follow-up write tools).
/// </summary>
public static class AssistantReplyFormatter
{
    public const int DefaultPreview = 5;
    public const int AgendaPreview = 8;
    public const int PracticalPreview = 6;
    public const int HardCap = 60;

    private static readonly string[] DayNames =
        ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private static readonly string[] Months =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    ];

    public static string DayTitle(DateOnly d) =>
        $"{DayNames[(int)d.DayOfWeek]}, {d.Day} de {Months[d.Month - 1]}";

    public static string DayTitle(string iso) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? DayTitle(d)
            : iso;

    public static string DayShort(DateOnly d) => $"{DayNames[(int)d.DayOfWeek]} {d:yyyy-MM-dd}";

    public static string TimeRange(string start, string? end) =>
        string.IsNullOrWhiteSpace(end) ? start : $"{start} – {end}";

    public static string Occupancy(int occupied, int capacity) =>
        $"{occupied} de {capacity} ocupados";

    public static string Seats(int available, int capacity) =>
        $"{available} de {capacity} cupos libres";

    public static string Money(decimal value) =>
        "$" + value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');

    public static string StatusLabel(string status) => status switch
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

    public static string AgendaKind(string kind) => kind.ToLowerInvariant() switch
    {
        "theory" => "Clase teórica",
        "exam" => "Examen teórico",
        "practical" => "Clase de manejo",
        _ => kind
    };

    public static string MoreHint(int shown, int total, string what = "registros") =>
        total <= shown
            ? ""
            : $"Se muestran {shown} de {total} {what}. Si necesitas el resto, pide el listado completo.";

    public static int TakeCount(int total, bool full, int preview, int hardCap = HardCap) =>
        full ? Math.Min(total, hardCap) : Math.Min(total, Math.Min(preview, hardCap));

    /// <summary>School weekly agenda grouped by day.</summary>
    public static string FormatAgenda(
        SchoolAgendaDto agenda,
        IReadOnlyList<AgendaItemDto> items,
        bool fullList)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**Agenda** ({DayTitle(agenda.From)} – {DayTitle(agenda.To)})");
        sb.AppendLine($"{items.Count} actividad{(items.Count == 1 ? "" : "es")}.");

        if (items.Count == 0)
        {
            sb.AppendLine("No hay clases ni citas en ese rango.");
            return sb.ToString().TrimEnd();
        }

        var allSameStatus = items.Select(i => i.Status).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
        if (allSameStatus && items[0].Status is "Scheduled")
        {
            sb.AppendLine("Estado: programadas.");
        }

        var take = TakeCount(items.Count, fullList, AgendaPreview);
        var shown = items.Take(take).ToList();
        string? currentDay = null;
        foreach (var i in shown)
        {
            if (!string.Equals(currentDay, i.Date, StringComparison.Ordinal))
            {
                currentDay = i.Date;
                sb.AppendLine();
                sb.AppendLine($"**{DayTitle(i.Date)}**");
            }

            var kind = AgendaKind(i.Kind);
            var title = CleanAgendaTitle(i.Title, kind);
            sb.AppendLine($"- **{TimeRange(i.StartTime, i.EndTime)}** · {kind}{(string.IsNullOrWhiteSpace(title) ? "" : $" — {title}")}");
            if (!string.IsNullOrWhiteSpace(i.InstructorName))
            {
                sb.AppendLine($"  - Instructor: {i.InstructorName}");
            }
            else if (i.Kind is "theory" or "practical")
            {
                sb.AppendLine("  - Instructor: sin asignar");
            }

            if (i.Capacity > 0)
            {
                sb.AppendLine($"  - Ocupación: {Occupancy(i.Occupied, i.Capacity)}");
            }

            if (!allSameStatus || i.Status is not "Scheduled")
            {
                sb.AppendLine($"  - Estado: {StatusLabel(i.Status)}");
            }

            if (i.Students.Count > 0)
            {
                var names = string.Join(", ", i.Students.Take(6).Select(s => s.Name));
                if (i.Students.Count > 6)
                {
                    names += "…";
                }

                sb.AppendLine($"  - Estudiantes: {names}");
            }

            sb.AppendLine($"  - (clase_id={i.Id})");
        }

        var hint = MoreHint(shown.Count, items.Count, "actividades");
        if (!string.IsNullOrEmpty(hint))
        {
            sb.AppendLine();
            sb.AppendLine(hint);
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatTheoryWeek(
        DateOnly weekStart,
        DateOnly weekEnd,
        string dayTypeLabel,
        IReadOnlyList<TheoryClassSessionDto> sessions,
        bool fullList)
    {
        var ordered = sessions.OrderBy(x => x.SessionDate).ThenBy(x => x.StartTime).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"**Clases teóricas** ({DayTitle(weekStart)} – {DayTitle(weekEnd)})");
        sb.AppendLine($"Tu grupo: {dayTypeLabel}.");

        if (ordered.Count == 0)
        {
            sb.AppendLine("No hay clases programadas esa semana.");
            return sb.ToString().TrimEnd();
        }

        var take = TakeCount(ordered.Count, fullList, AgendaPreview);
        string? currentDay = null;
        foreach (var s in ordered.Take(take))
        {
            if (currentDay != DayTitle(s.SessionDate))
            {
                currentDay = DayTitle(s.SessionDate);
                sb.AppendLine();
                sb.AppendLine($"**{currentDay}**");
            }

            sb.AppendLine($"- **{TimeRange(s.StartTime, s.EndTime)}** · {s.TopicName}");
            sb.AppendLine($"  - Aula: {s.ClassroomName}");
            if (!string.IsNullOrWhiteSpace(s.InstructorName))
            {
                sb.AppendLine($"  - Instructor: {s.InstructorName}");
            }

            sb.AppendLine($"  - Cupos: {Seats(s.AvailableSeats, s.Capacity)}");
            sb.AppendLine($"  - Estado: {StatusLabel(s.Status)}");
            if (s.MyReservationId is int rid)
            {
                sb.AppendLine($"  - Ya la tienes reservada (reserva_id={rid})");
            }
            else if (!string.IsNullOrWhiteSpace(s.BookingMessage))
            {
                sb.AppendLine($"  - Reserva: {s.BookingMessage}");
            }
            else if (!string.IsNullOrWhiteSpace(s.BookingState))
            {
                sb.AppendLine($"  - Reserva: {s.BookingState}");
            }

            sb.AppendLine($"  - (clase_id={s.Id})");
        }

        var hint = MoreHint(Math.Min(take, ordered.Count), ordered.Count, "clases");
        if (!string.IsNullOrEmpty(hint))
        {
            sb.AppendLine();
            sb.AppendLine(hint);
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatStudentsProgress(
        IReadOnlyList<EnrollmentDto> list,
        string? filter,
        bool fullList)
    {
        var sb = new StringBuilder();
        var filterLabel = FilterLabel(filter);
        sb.AppendLine(string.IsNullOrEmpty(filterLabel)
            ? $"**Estudiantes:** {list.Count}"
            : $"**Estudiantes — {filterLabel}:** {list.Count}");

        if (list.Count == 0)
        {
            sb.AppendLine("No hay resultados con ese criterio.");
            return sb.ToString().TrimEnd();
        }

        var take = TakeCount(list.Count, fullList, DefaultPreview);
        foreach (var x in list.Take(take))
        {
            var e = x.PracticalEligibility;
            var hours = e is null
                ? "horas sin dato"
                : $"teoría {e.TheoryHoursCompleted}/{e.TheoryHoursRequired} h, taller {e.WorkshopHoursCompleted}/{e.WorkshopHoursRequired} h";
            var exam = e?.TheoryExamPassed == true
                ? "examen aprobado"
                : x.TheoryExamAuthorized
                    ? "examen autorizado"
                    : "examen no autorizado";
            var practical = x.PracticalAuthorized ? "manejo autorizado" : "manejo no autorizado";

            sb.AppendLine($"- **{x.StudentName}**");
            sb.AppendLine($"  - Matrícula: {EnrollmentStatus(x.Status)}");
            sb.AppendLine($"  - {hours}");
            sb.AppendLine($"  - {exam} · {practical}");
            if (x.BalanceDue > 0)
            {
                sb.AppendLine($"  - Saldo: {Money(x.BalanceDue)}");
            }

            sb.AppendLine($"  - (estudiante_id={x.StudentUserId})");
        }

        var hint = MoreHint(Math.Min(take, list.Count), list.Count, "estudiantes");
        if (!string.IsNullOrEmpty(hint))
        {
            sb.AppendLine();
            sb.AppendLine(hint);
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatSchoolSummary(
        int apprentices,
        int pending,
        int balanceCount,
        decimal balanceTotal,
        int readyExam,
        int noExamAppt,
        int readyPractical,
        int examsNext7,
        IEnumerable<(string Name, int Id)> topReady,
        IEnumerable<(string Name, int Id)> topNoAppt,
        IEnumerable<(string Name, decimal Balance)> topBalance)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Resumen de la escuela**");
        sb.AppendLine($"• Estudiantes: {apprentices}");
        sb.AppendLine($"• Matrículas pendientes: {pending}");
        sb.AppendLine($"• Con saldo: {balanceCount} (total {Money(balanceTotal)})");
        sb.AppendLine($"• Listos para examen: {readyExam}");
        sb.AppendLine($"• Sin cita de examen: {noExamAppt}");
        sb.AppendLine($"• Listos para manejo: {readyPractical}");
        sb.AppendLine($"• Exámenes en los próximos 7 días: {examsNext7}");

        AppendNameList(sb, "Listos para examen", topReady.Select(x => $"{x.Name} (estudiante_id={x.Id})"));
        AppendNameList(sb, "Sin cita de examen", topNoAppt.Select(x => $"{x.Name} (estudiante_id={x.Id})"));
        AppendNameList(sb, "Mayores saldos", topBalance.Select(x => $"{x.Name} {Money(x.Balance)}"));

        return sb.ToString().TrimEnd();
    }

    public static string FormatExamSlotsText(
        string from,
        string to,
        bool canBook,
        string? blockReason,
        string? myBookingLine,
        IReadOnlyList<(string Date, IReadOnlyList<(string Time, int Available)> Slots)> days,
        bool fullList)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Cupos de examen teórico**");
        sb.AppendLine($"Rango: {DayTitle(from)} – {DayTitle(to)}.");
        if (!string.IsNullOrWhiteSpace(blockReason))
        {
            sb.AppendLine(blockReason);
        }

        sb.AppendLine($"Puede agendar: {(canBook ? "sí" : "no")}.");
        if (!string.IsNullOrWhiteSpace(myBookingLine))
        {
            sb.AppendLine(myBookingLine);
        }

        var withSlots = days.Where(d => d.Slots.Count > 0).ToList();
        if (withSlots.Count == 0)
        {
            sb.AppendLine("No hay cupos libres en ese rango.");
            return sb.ToString().TrimEnd();
        }

        var take = TakeCount(withSlots.Count, fullList, DefaultPreview);
        foreach (var (date, slots) in withSlots.Take(take))
        {
            sb.AppendLine();
            sb.AppendLine($"**{DayTitle(date)}**");
            foreach (var (time, available) in slots)
            {
                sb.AppendLine($"- **{time}** · {available} cupo{(available == 1 ? "" : "s")}");
            }
        }

        var hint = MoreHint(Math.Min(take, withSlots.Count), withSlots.Count, "días con cupo");
        if (!string.IsNullOrEmpty(hint))
        {
            sb.AppendLine();
            sb.AppendLine(hint);
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatPracticalDashboard(
        string progressLine,
        string canBookLine,
        IEnumerable<string> notes,
        IReadOnlyList<(int ReservationId, string Kind, string Date, string Start, string End, string Instructor, string Vehicle)> reservations,
        IReadOnlyList<(int Id, string Kind, string Date, string Start, string End, string Instructor, string Vehicle, string BookingState)> lessons,
        IReadOnlyList<(int Id, string Kind, string Date, string Start, string End, string Instructor, string Vehicle, string BookingState)> exams,
        bool fullList)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Clases de manejo**");
        sb.AppendLine($"• {progressLine}");
        sb.AppendLine($"• {canBookLine}");
        foreach (var n in notes)
        {
            if (!string.IsNullOrWhiteSpace(n))
            {
                sb.AppendLine($"• {n}");
            }
        }

        if (reservations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Tus reservas**");
            foreach (var r in reservations.Take(10))
            {
                sb.AppendLine($"- **{TimeRange(r.Start, r.End)}** · {r.Kind} — {DayTitle(r.Date)}");
                sb.AppendLine($"  - Instructor: {r.Instructor}");
                sb.AppendLine($"  - Vehículo: {r.Vehicle}");
                sb.AppendLine($"  - (reserva_id={r.ReservationId})");
            }
        }

        var open = lessons.Concat(exams).ToList();
        if (open.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("No hay cupos de manejo publicados.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine();
        sb.AppendLine("**Cupos disponibles**");
        var take = TakeCount(open.Count, fullList, PracticalPreview);
        string? day = null;
        foreach (var l in open.Take(take))
        {
            if (!string.Equals(day, l.Date, StringComparison.Ordinal))
            {
                day = l.Date;
                sb.AppendLine();
                sb.AppendLine($"**{DayTitle(l.Date)}**");
            }

            sb.AppendLine($"- **{TimeRange(l.Start, l.End)}** · {l.Kind}");
            sb.AppendLine($"  - Instructor: {l.Instructor}");
            sb.AppendLine($"  - Vehículo: {l.Vehicle}");
            if (!string.IsNullOrWhiteSpace(l.BookingState))
            {
                sb.AppendLine($"  - {l.BookingState}");
            }

            sb.AppendLine($"  - (clase_id={l.Id})");
        }

        var hint = MoreHint(Math.Min(take, open.Count), open.Count, "cupos");
        if (!string.IsNullOrEmpty(hint))
        {
            sb.AppendLine();
            sb.AppendLine(hint);
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatPracticalOptions(
        IReadOnlyList<(int Id, string Label)> vehicles,
        IReadOnlyList<(int Id, string Name)> instructors,
        IReadOnlyList<(int Id, string Name, string Progress, bool ClassOk, bool ExamOk, string? Block)> students,
        bool fullList)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Opciones para manejo**");
        sb.AppendLine();
        sb.AppendLine("**Vehículos**");
        if (vehicles.Count == 0)
        {
            sb.AppendLine("- ninguno activo");
        }
        else
        {
            foreach (var v in vehicles)
            {
                sb.AppendLine($"- {v.Label} (vehiculo_id={v.Id})");
            }
        }

        sb.AppendLine();
        sb.AppendLine("**Instructores**");
        if (instructors.Count == 0)
        {
            sb.AppendLine("- ninguno");
        }
        else
        {
            foreach (var i in instructors)
            {
                sb.AppendLine($"- {i.Name} (instructor_id={i.Id})");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"**Estudiantes autorizados:** {students.Count}");
        var take = TakeCount(students.Count, fullList, DefaultPreview);
        foreach (var s in students.Take(take))
        {
            sb.AppendLine($"- **{s.Name}** — {s.Progress}");
            sb.AppendLine($"  - Puede clase: {(s.ClassOk ? "sí" : "no")} · Puede examen: {(s.ExamOk ? "sí" : "no")}");
            if (!string.IsNullOrWhiteSpace(s.Block))
            {
                sb.AppendLine($"  - {s.Block}");
            }

            sb.AppendLine($"  - (estudiante_id={s.Id})");
        }

        var hint = MoreHint(Math.Min(take, students.Count), students.Count, "estudiantes");
        if (!string.IsNullOrEmpty(hint))
        {
            sb.AppendLine();
            sb.AppendLine(hint);
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatStudentDetail(
        string name,
        int studentId,
        string category,
        string group,
        string enrollment,
        decimal paid,
        decimal due,
        decimal balance,
        string theoryHours,
        string workshopHours,
        bool examAuthorized,
        bool examPassed,
        bool practicalAuthorized,
        string? examAppointment,
        string practicalProgress,
        string? nextPractical,
        string? blockReason)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**Expediente — {name}** (estudiante_id={studentId})");
        sb.AppendLine($"• Categoría: {category}");
        sb.AppendLine($"• Grupo: {group}");
        sb.AppendLine($"• Matrícula: {enrollment}");
        sb.AppendLine($"• Pagos: {Money(paid)} de {Money(due)} · Saldo {Money(balance)}");
        sb.AppendLine($"• Teoría: {theoryHours}");
        sb.AppendLine($"• Taller: {workshopHours}");
        sb.AppendLine($"• Examen teórico: {(examAuthorized ? "autorizado" : "no autorizado")}, {(examPassed ? "aprobado" : "aún no aprobado")}");
        sb.AppendLine($"• Manejo: {(practicalAuthorized ? "autorizado" : "no autorizado")}");
        sb.AppendLine(examAppointment is null ? "• Sin cita de examen teórico" : $"• Cita de examen: {examAppointment}");
        sb.AppendLine($"• Práctica: {practicalProgress}");
        if (!string.IsNullOrWhiteSpace(nextPractical))
        {
            sb.AppendLine($"• Próxima clase de manejo: {nextPractical}");
        }

        if (!string.IsNullOrWhiteSpace(blockReason))
        {
            sb.AppendLine($"• Lo que falta: {blockReason}");
        }

        sb.AppendLine("El documento y el celular no se comparten con el asistente; están en la ficha del estudiante.");
        return sb.ToString().TrimEnd();
    }

    private static void AppendNameList(StringBuilder sb, string title, IEnumerable<string> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine($"**{title}**");
        foreach (var item in list)
        {
            sb.AppendLine($"- {item}");
        }
    }

    private static string CleanAgendaTitle(string title, string kind)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "";
        }

        var t = title.Trim();
        if (t.Equals(kind, StringComparison.OrdinalIgnoreCase)
            || t.Equals("Clase teórica", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Clase de manejo", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return t;
    }

    private static string FilterLabel(string? filter) => (filter ?? "").Trim().ToLowerInvariant() switch
    {
        "listos_examen" => "listos para examen",
        "autorizados_examen" => "examen autorizado",
        "listos_manejo" => "listos para manejo",
        "en_manejo" => "en manejo",
        "con_saldo" => "con saldo pendiente",
        "pendientes" => "matrícula pendiente",
        "faltan_horas" => "faltan horas",
        "todos" or "" => "",
        _ => filter!.Trim()
    };

    private static string EnrollmentStatus(string status) => status switch
    {
        "Active" or "Accepted" => "autorizada",
        "Suspended" => "suspendida",
        _ => "pendiente"
    };
}
