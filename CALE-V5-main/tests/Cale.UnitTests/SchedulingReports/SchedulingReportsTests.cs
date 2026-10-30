using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Application.WhatsApp;
using Microsoft.Extensions.Options;

namespace Cale.UnitTests.SchedulingReports;

public sealed class SchedulingReportsTests
{
    [Fact]
    public void Options_default_gates_block_whatsapp_and_jobs()
    {
        var opts = new SchedulingReportsOptions();
        Assert.False(opts.Enabled);
        Assert.True(opts.AllowManualDownload);
        Assert.True(opts.CanManualDownload);
        Assert.False(opts.AllowManualWhatsAppSend);
        Assert.False(opts.CanManualWhatsAppSend);
        Assert.False(opts.EnableDailyFullReportJob);
        Assert.False(opts.EnableDailyChangesReportJob);
        Assert.False(opts.DailyJobsArmed);
        Assert.False(opts.WhatsApp.IsConfigured);
    }

    [Fact]
    public void Options_whatsapp_requires_all_flags_and_credentials()
    {
        var opts = new SchedulingReportsOptions
        {
            Enabled = true,
            AllowManualWhatsAppSend = true,
            WhatsApp =
            {
                Enabled = true,
                PhoneNumberId = "123",
                AccessToken = "token"
            }
        };
        Assert.True(opts.CanManualWhatsAppSend);

        opts.WhatsApp.AccessToken = "";
        Assert.False(opts.CanManualWhatsAppSend);
    }

    [Fact]
    public async Task Disabled_whatsapp_sender_never_succeeds()
    {
        var sender = new DisabledWhatsAppCloudSender(Options.Create(new SchedulingReportsOptions
        {
            Enabled = true,
            AllowManualWhatsAppSend = true,
            WhatsApp = { Enabled = true, PhoneNumberId = "1", AccessToken = "x" }
        }));

        var result = await sender.SendDocumentAsync(new WhatsAppDocumentSendRequest(
            "+573001112233",
            "a.xlsx",
            [1, 2, 3],
            null));

        Assert.False(result.Success);
        Assert.Equal("NotConfigured", result.Status);
        Assert.Null(result.MessageId);
    }

    [Fact]
    public void Diff_detects_alta_baja_and_reprogramacion()
    {
        var diff = new SchedulingReportDiffService();
        var baseline = new List<AgendaItemDto>
        {
            Item("theory", 1, "2026-10-28", "08:00", "Normas"),
            Item("practical", 2, "2026-10-28", "10:00", "Manejo A")
        };
        var current = new List<AgendaItemDto>
        {
            Item("theory", 1, "2026-10-28", "09:00", "Normas"),
            Item("exam", 0, "2026-10-28", "14:00", "Examen teórico")
        };

        var json = diff.SerializeSnapshot(baseline);
        var result = diff.Diff(json, current);

        Assert.True(result.HasChanges);
        Assert.Contains(result.Changes, c => c.ChangeKind == "Reprogramación" && c.Id == 1);
        Assert.Contains(result.Changes, c => c.ChangeKind == "Baja" && c.Id == 2);
        Assert.Contains(result.Changes, c => c.ChangeKind == "Alta" && c.Kind == "exam");
    }

    [Fact]
    public void Diff_empty_when_identical()
    {
        var diff = new SchedulingReportDiffService();
        var items = new List<AgendaItemDto>
        {
            Item("theory", 1, "2026-10-28", "08:00", "Normas")
        };
        var json = diff.SerializeSnapshot(items);
        var result = diff.Diff(json, items);
        Assert.False(result.HasChanges);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void Generator_builds_workbook_with_expected_sheets()
    {
        var items = new List<AgendaItemDto>
        {
            Item("theory", 1, "2026-10-28", "08:00", "Normas", "Ana", 20, 5),
            Item("practical", 2, "2026-10-28", "10:00", "Manejo", "Luis", 1, 1),
            Item("exam", 0, "2026-10-28", "14:00", "Examen teórico", null, 10, 3)
        };

        var bytes = SchedulingReportGenerator.BuildWorkbook(new DateOnly(2026, 10, 28), items);
        Assert.True(bytes.Length > 500);

        using var ms = new MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        Assert.Contains(wb.Worksheets, w => w.Name == "Resumen");
        Assert.Contains(wb.Worksheets, w => w.Name == "Teoría");
        Assert.Contains(wb.Worksheets, w => w.Name == "Manejo");
        Assert.Contains(wb.Worksheets, w => w.Name == "Exámenes");
        Assert.Equal("Teoría", wb.Worksheet("Teoría").Cell(2, 1).GetString());
        Assert.Equal("Sin estudiantes inscritos", wb.Worksheet("Teoría").Cell(2, 11).GetString());
        Assert.Equal("Nombre completo", wb.Worksheet("Teoría").Cell(1, 11).GetString());
    }

    [Fact]
    public void Generator_theory_lists_enrolled_student_details()
    {
        var student = new AgendaStudentDto(
            9,
            "Ana Pérez",
            "Reserved",
            "1234567890",
            "3001112233",
            new DateTime(2026, 10, 27, 15, 30, 0, DateTimeKind.Utc));
        var items = new List<AgendaItemDto>
        {
            new(
                "theory",
                1,
                "2026-10-28",
                "08:00",
                "09:59",
                "Normas · Aula 1",
                "Luis",
                "Scheduled",
                20,
                1,
                19,
                [student])
        };

        var bytes = SchedulingReportGenerator.BuildWorkbook(new DateOnly(2026, 10, 28), items);
        using var ms = new MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        var sheet = wb.Worksheet("Teoría");
        Assert.Equal("Ana Pérez", sheet.Cell(2, 11).GetString());
        Assert.Equal("1234567890", sheet.Cell(2, 12).GetString());
        Assert.Equal("3001112233", sheet.Cell(2, 13).GetString());
        Assert.Equal("Reserved", sheet.Cell(2, 14).GetString());
        Assert.Contains("2026-10-27", sheet.Cell(2, 10).GetString(), StringComparison.Ordinal);
        Assert.Equal("Normas · Aula 1", sheet.Cell(2, 4).GetString());
    }

    [Fact]
    public void Generator_inserts_blank_row_between_time_slots()
    {
        var items = new List<AgendaItemDto>
        {
            new("theory", 1, "2026-10-28", "08:00", "09:59", "Normas", "Ana", "Scheduled", 20, 0, 20, []),
            new("theory", 2, "2026-10-28", "10:00", "11:59", "Señales", "Ana", "Scheduled", 20, 0, 20, [])
        };

        var bytes = SchedulingReportGenerator.BuildWorkbook(new DateOnly(2026, 10, 28), items);
        using var ms = new MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        var sheet = wb.Worksheet("Teoría");
        Assert.Equal("08:00", sheet.Cell(2, 2).GetString());
        Assert.True(string.IsNullOrWhiteSpace(sheet.Cell(3, 1).GetString()));
        Assert.Equal("10:00", sheet.Cell(4, 2).GetString());
    }

    [Fact]
    public void Generator_only_with_enrollments_skips_empty_slots()
    {
        var student = new AgendaStudentDto(9, "Ana Pérez", "Reserved", "1", "300", DateTime.UtcNow);
        var withPeople = new AgendaItemDto(
            "theory", 1, "2026-10-28", "08:00", "09:59", "Normas", "Luis", "Scheduled", 20, 1, 19, [student]);
        var empty = new AgendaItemDto(
            "theory", 2, "2026-10-28", "10:00", "11:59", "Señales", "Luis", "Scheduled", 20, 0, 20, []);
        var filtered = new[] { withPeople, empty }
            .Where(x => x.Occupied > 0 || x.Students.Count > 0)
            .ToList();

        var bytes = SchedulingReportGenerator.BuildWorkbook(new DateOnly(2026, 10, 28), filtered);
        using var ms = new MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        var sheet = wb.Worksheet("Teoría");
        Assert.Equal("Ana Pérez", sheet.Cell(2, 11).GetString());
        Assert.Equal("08:00", sheet.Cell(2, 2).GetString());
        // No second theory block for the empty 10:00 slot.
        Assert.True(string.IsNullOrWhiteSpace(sheet.Cell(4, 2).GetString()));
    }

    [Fact]
    public void Generator_empty_day_still_produces_file()
    {
        var bytes = SchedulingReportGenerator.BuildWorkbook(new DateOnly(2026, 10, 28), []);
        Assert.True(bytes.Length > 200);
        using var ms = new MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        Assert.Contains(
            "Sin programación",
            wb.Worksheet("Resumen").Cell(11, 1).GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static AgendaItemDto Item(
        string kind,
        int id,
        string date,
        string start,
        string title,
        string? instructor = null,
        int capacity = 10,
        int occupied = 0) =>
        new(
            kind,
            id,
            date,
            start,
            null,
            title,
            instructor,
            "scheduled",
            capacity,
            occupied,
            Math.Max(0, capacity - occupied),
            []);
}
