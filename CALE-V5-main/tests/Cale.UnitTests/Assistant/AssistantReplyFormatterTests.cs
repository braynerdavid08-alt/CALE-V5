using Cale.Api.Services.Assistant;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;

namespace Cale.UnitTests.Assistant;

public sealed class AssistantReplyFormatterTests
{
    [Fact]
    public void Simple_progress_style_reply_stays_brief_after_clean()
    {
        const string text = """
            **Tu formación**
            • Teoría: 10 de 20 h
            • Saldo pendiente: $0
            """;

        var clean = AssistantService.FinalizeReply(text);

        Assert.Contains("Teoría: 10 de 20 h", clean);
        Assert.DoesNotContain("|", clean);
        Assert.True(clean.Length < 200);
    }

    [Fact]
    public void Agenda_groups_by_day_and_avoids_pipe_dumps()
    {
        var agenda = new SchoolAgendaDto("2026-10-30", "2026-10-31", []);
        var items = new List<AgendaItemDto>
        {
            new("theory", 1, "2026-10-30", "18:00", "20:00", "Normas · Aula 1", null, "Scheduled", 20, 0, 20, []),
            new("theory", 2, "2026-10-31", "08:00", "09:59", "Señales · Aula 1", null, "Scheduled", 20, 0, 20, [])
        };

        var text = AssistantReplyFormatter.FormatAgenda(agenda, items, fullList: true);
        var user = AssistantService.CleanForUser(text);

        Assert.Contains("**Agenda**", text);
        Assert.Contains("**viernes, 30 de octubre**", text);
        Assert.Contains("**sábado, 31 de octubre**", text);
        Assert.Contains("18:00 – 20:00", text);
        Assert.Contains("Ocupación: 0 de 20 ocupados", text);
        Assert.Contains("Instructor: sin asignar", text);
        Assert.DoesNotContain("programada", user.Split('\n').First(l => l.Contains("18:00")));
        Assert.DoesNotContain("|", user);
        Assert.DoesNotContain("clase_id", user);
    }

    [Fact]
    public void Many_students_preview_shows_count_and_offers_more()
    {
        var list = Enumerable.Range(1, 12)
            .Select(i => new EnrollmentDto(
                i, i, $"Estudiante {i}", $"e{i}@x.com", StudentEnrollmentStatuses.Active,
                "Weekday", null, "B1", false, false, DateTime.UtcNow, null,
                new PracticalEligibilityDto(
                    false, false, true, true, 20, 20, 10, 10, false, false, null),
                BalanceDue: 0))
            .ToList();

        var preview = AssistantReplyFormatter.FormatStudentsProgress(list, "listos_examen", fullList: false);
        var full = AssistantReplyFormatter.FormatStudentsProgress(list, "listos_examen", fullList: true);

        Assert.Contains("12", preview);
        Assert.Contains("Se muestran 5 de 12", preview);
        Assert.Contains("listado completo", preview);
        Assert.DoesNotContain("Se muestran 5 de 12", full);
        Assert.Equal(12, full.Split("estudiante_id=", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Full_list_request_keeps_all_organized_rows()
    {
        var list = Enumerable.Range(1, 7)
            .Select(i => new EnrollmentDto(
                i, i, $"Alumno {i}", $"a{i}@x.com", StudentEnrollmentStatuses.Active,
                null, null, null, false, false, DateTime.UtcNow, null, null, i * 1000m))
            .ToList();

        var text = AssistantReplyFormatter.FormatStudentsProgress(list, "con_saldo", fullList: true);
        var user = AssistantService.CleanForUser(text);

        Assert.Contains("con saldo pendiente", text);
        Assert.Contains("Saldo: $7.000", user);
        Assert.Equal(7, user.Split("**Alumno ", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("estudiante_id", user);
    }

    [Fact]
    public void Optional_empty_fields_stay_readable()
    {
        var agenda = new SchoolAgendaDto("2026-10-30", "2026-10-30", []);
        var items = new List<AgendaItemDto>
        {
            new("theory", 9, "2026-10-30", "10:00", null, "Clase teórica", null, "Scheduled", 15, 2, 13, [])
        };

        var text = AssistantReplyFormatter.FormatAgenda(agenda, items, true);

        Assert.Contains("10:00", text);
        Assert.DoesNotContain("10:00 –", text);
        Assert.Contains("Instructor: sin asignar", text);
        Assert.Contains("2 de 15 ocupados", text);
    }

    [Fact]
    public void Student_detail_hides_document_and_phone()
    {
        var text = AssistantReplyFormatter.FormatStudentDetail(
            "Ana Pérez", 42, "A2", "sábados", "autorizada",
            500_000, 800_000, 300_000,
            "15/20 h", "8/10 h",
            true, false, false,
            "2026-11-05 a las 09:00",
            "3/15 clases, 1 programadas",
            null,
            "Falta aprobar el examen teórico");

        var user = AssistantService.CleanForUser(text);

        Assert.Contains("Expediente — Ana Pérez", user);
        Assert.Contains("Saldo $300.000", user);
        Assert.Contains("documento y el celular no se comparten", user);
        Assert.DoesNotContain("estudiante_id", user);
        Assert.DoesNotContain("cédula", user.ToLowerInvariant());
        Assert.DoesNotContain("celular:", user.ToLowerInvariant());
    }

    [Fact]
    public void Attendance_hours_and_amounts_use_consistent_labels()
    {
        var text = AssistantReplyFormatter.FormatSchoolSummary(
            40, 2, 5, 1_250_000m, 8, 3, 4, 2,
            [("Ana", 1), ("Luis", 2)],
            [("Marta", 3)],
            [("Pedro", 400_000m)]);

        var user = AssistantService.CleanForUser(text);

        Assert.Contains("Estudiantes: 40", user);
        Assert.Contains("total $1.250.000", user);
        Assert.Contains("Listos para examen", user);
        Assert.Contains("Ana", user);
        Assert.DoesNotContain("estudiante_id", user);
        Assert.DoesNotContain("|", user);
    }

    [Fact]
    public void Cartera_rows_keep_money_visible_after_clean()
    {
        var list = new List<EnrollmentDto>
        {
            new(1, 10, "Carla", "c@x.com", StudentEnrollmentStatuses.Active,
                null, null, "B1", true, false, DateTime.UtcNow, null, null, 250_000m)
        };

        var text = AssistantReplyFormatter.FormatStudentsProgress(list, "con_saldo", true);
        var user = AssistantService.CleanForUser(text);

        Assert.Contains("Saldo: $250.000", user);
        Assert.Contains("examen autorizado", user);
    }

    [Fact]
    public void Dates_hours_and_quantities_are_human_readable()
    {
        var sessions = new List<TheoryClassSessionDto>
        {
            new(5, new DateOnly(2026, 10, 30), "07:00", "09:00", 1, "Normas", "#0f0",
                1, "Aula 1", 20, 3, 17, "Scheduled", null, null, null,
                DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "open", null, null, null)
        };

        var text = AssistantReplyFormatter.FormatTheoryWeek(
            new DateOnly(2026, 10, 27),
            new DateOnly(2026, 11, 2),
            "semana (lunes a viernes)",
            sessions,
            true);

        Assert.Contains("**viernes, 30 de octubre**", text);
        Assert.Contains("07:00 – 09:00", text);
        Assert.Contains("17 de 20 cupos libres", text);
        Assert.Contains("(clase_id=5)", text);
        Assert.DoesNotContain("|", AssistantService.CleanForUser(text));
    }

    [Fact]
    public void Long_mobile_friendly_reply_uses_line_breaks_not_one_paragraph()
    {
        var items = Enumerable.Range(0, 10)
            .Select(i => new AgendaItemDto(
                "theory",
                i + 1,
                $"2026-10-{21 + i:00}",
                "08:00",
                "10:00",
                $"Tema {i}",
                null,
                "Scheduled",
                20,
                i,
                20 - i,
                []))
            .ToList();

        var text = AssistantReplyFormatter.FormatAgenda(
            new SchoolAgendaDto("2026-10-21", "2026-10-30", items),
            items,
            fullList: false);

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 12);
        Assert.Contains("Se muestran 8 de 10", text);
        Assert.Contains("listado completo", text);
        Assert.DoesNotContain(" | ", text);
        Assert.True(lines.Max(l => l.Length) < 120, "lines should wrap naturally on narrow phones");
    }
}
