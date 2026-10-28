using System.Globalization;
using ClosedXML.Excel;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed record SchedulingReportFile(
    byte[] Bytes,
    string FileName,
    DateOnly TargetDate,
    string Type,
    int ItemCount,
    long RunId);

/// <summary>
/// Builds a day-scoped Excel workbook from SchoolAgendaService and records a SchedulingReportRun.
/// </summary>
public sealed class SchedulingReportGenerator
{
    private static readonly CultureInfo EsCo = CultureInfo.GetCultureInfo("es-CO");

    private readonly SchoolAgendaService _agenda;
    private readonly SchedulingReportDiffService _diff;
    private readonly CaleDbContext _db;
    private readonly SchedulingReportsOptions _options;

    public SchedulingReportGenerator(
        SchoolAgendaService agenda,
        SchedulingReportDiffService diff,
        CaleDbContext db,
        IOptions<SchedulingReportsOptions> options)
    {
        _agenda = agenda;
        _diff = diff;
        _db = db;
        _options = options.Value;
    }

    public async Task<SchedulingReportFile> GenerateDayAsync(
        int schoolUserId,
        DateOnly targetDate,
        string reportType,
        CancellationToken ct)
    {
        var agenda = await _agenda.GetAsync(schoolUserId, targetDate, targetDate, ct);
        var bytes = BuildWorkbook(targetDate, agenda.Items);
        var fileName = $"programacion-{targetDate:yyyy-MM-dd}.xlsx";

        var run = new SchedulingReportRun
        {
            SchoolUserId = schoolUserId,
            Type = reportType,
            TargetDate = targetDate,
            Status = SchedulingReportStatuses.Generated,
            FileName = fileName,
            ByteLength = bytes.Length,
            CreatedAt = DateTime.UtcNow
        };
        _db.Set<SchedulingReportRun>().Add(run);
        await _db.SaveChangesAsync(ct);

        return new SchedulingReportFile(bytes, fileName, targetDate, reportType, agenda.Items.Count, run.Id);
    }

    public async Task SaveTwoPmBaselineAsync(
        int schoolUserId,
        DateOnly targetDate,
        CancellationToken ct)
    {
        var agenda = await _agenda.GetAsync(schoolUserId, targetDate, targetDate, ct);
        var payload = _diff.SerializeSnapshot(agenda.Items);
        var hash = _diff.HashPayload(payload);

        var existing = await _db.Set<SchedulingReportSnapshot>()
            .FirstOrDefaultAsync(
                x => x.SchoolUserId == schoolUserId
                    && x.TargetDate == targetDate
                    && x.CutKind == SchedulingReportCutKinds.TwoPmBaseline,
                ct);

        if (existing is null)
        {
            _db.Set<SchedulingReportSnapshot>().Add(new SchedulingReportSnapshot
            {
                SchoolUserId = schoolUserId,
                CutKind = SchedulingReportCutKinds.TwoPmBaseline,
                TargetDate = targetDate,
                PayloadJson = payload,
                PayloadHash = hash,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.PayloadJson = payload;
            existing.PayloadHash = hash;
            existing.CreatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<(SchedulingReportFile? File, SchedulingReportDiffResult Diff)> TryGenerateChangesAsync(
        int schoolUserId,
        DateOnly targetDate,
        CancellationToken ct)
    {
        var baseline = await _db.Set<SchedulingReportSnapshot>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.SchoolUserId == schoolUserId
                    && x.TargetDate == targetDate
                    && x.CutKind == SchedulingReportCutKinds.TwoPmBaseline,
                ct);

        var agenda = await _agenda.GetAsync(schoolUserId, targetDate, targetDate, ct);
        var diff = _diff.Diff(baseline?.PayloadJson ?? "[]", agenda.Items);

        if (!diff.HasChanges && !_options.EmptyChangesReports)
        {
            return (null, diff);
        }

        var bytes = BuildChangesWorkbook(targetDate, diff);
        var fileName = $"novedades-{targetDate:yyyy-MM-dd}.xlsx";
        var run = new SchedulingReportRun
        {
            SchoolUserId = schoolUserId,
            Type = SchedulingReportTypes.DailyChanges,
            TargetDate = targetDate,
            Status = SchedulingReportStatuses.Generated,
            FileName = fileName,
            ByteLength = bytes.Length,
            CreatedAt = DateTime.UtcNow
        };
        _db.Set<SchedulingReportRun>().Add(run);
        await _db.SaveChangesAsync(ct);

        return (
            new SchedulingReportFile(
                bytes,
                fileName,
                targetDate,
                SchedulingReportTypes.DailyChanges,
                diff.Changes.Count,
                run.Id),
            diff);
    }

    public static byte[] BuildWorkbook(DateOnly targetDate, IReadOnlyList<AgendaItemDto> items)
    {
        using var wb = new XLWorkbook();
        var theory = items.Where(x => x.Kind == "theory").ToList();
        var practical = items.Where(x => x.Kind is "practical" or "driving").ToList();
        var exams = items.Where(x => x.Kind is "exam" or "theory-exam").ToList();
        // Fallback: anything not already bucketed
        var other = items
            .Where(x => x.Kind is not ("theory" or "practical" or "driving" or "exam" or "theory-exam"))
            .ToList();

        WriteSummary(wb.Worksheets.Add("Resumen"), targetDate, items, theory.Count, practical.Count, exams.Count + other.Count);
        WriteItems(wb.Worksheets.Add("Teoría"), theory);
        WriteItems(wb.Worksheets.Add("Manejo"), practical);
        WriteItems(wb.Worksheets.Add("Exámenes"), exams.Concat(other).ToList());

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] BuildChangesWorkbook(DateOnly targetDate, SchedulingReportDiffResult diff)
    {
        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("Novedades");
        sheet.Cell(1, 1).Value = "Reporte de novedades";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = "Fecha (Colombia)";
        sheet.Cell(2, 2).Value = targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        sheet.Cell(3, 1).Value = "Cambios";
        sheet.Cell(3, 2).Value = diff.Changes.Count;

        var headers = new[]
        {
            "Cambio", "Tipo", "Id", "Fecha", "Hora", "Título", "Instructor", "Estado", "Cupos", "Ocupados", "Detalle"
        };
        const int headerRow = 5;
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(headerRow, c + 1).Value = headers[c];
            sheet.Cell(headerRow, c + 1).Style.Font.Bold = true;
        }

        var row = headerRow + 1;
        foreach (var ch in diff.Changes)
        {
            sheet.Cell(row, 1).Value = ch.ChangeKind;
            sheet.Cell(row, 2).Value = KindLabel(ch.Kind);
            sheet.Cell(row, 3).Value = ch.Id;
            sheet.Cell(row, 4).Value = ch.Date;
            sheet.Cell(row, 5).Value = ch.StartTime;
            sheet.Cell(row, 6).Value = ch.Title;
            sheet.Cell(row, 7).Value = ch.InstructorName ?? "";
            sheet.Cell(row, 8).Value = ch.Status;
            sheet.Cell(row, 9).Value = ch.Capacity;
            sheet.Cell(row, 10).Value = ch.Occupied;
            sheet.Cell(row, 11).Value = ch.Detail;
            row++;
        }

        if (diff.Changes.Count == 0)
        {
            sheet.Cell(row, 1).Value = "Sin novedades respecto al corte de las 14:00.";
        }

        var used = sheet.Range(headerRow, 1, Math.Max(headerRow, row - 1), headers.Length);
        used.SetAutoFilter();
        sheet.Columns().AdjustToContents(1, 60);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteSummary(
        IXLWorksheet sheet,
        DateOnly targetDate,
        IReadOnlyList<AgendaItemDto> items,
        int theoryCount,
        int practicalCount,
        int examCount)
    {
        sheet.Cell(1, 1).Value = "Reporte de programación";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = "Fecha (Colombia)";
        sheet.Cell(2, 2).Value = targetDate.ToString("dddd d 'de' MMMM yyyy", EsCo);
        sheet.Cell(2, 3).Value = targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        sheet.Cell(3, 1).Value = "Generado (Colombia)";
        sheet.Cell(3, 2).Value = ColombiaTime.NowInColombia().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        sheet.Cell(5, 1).Value = "Resumen";
        sheet.Cell(5, 1).Style.Font.Bold = true;
        sheet.Cell(6, 1).Value = "Total ítems";
        sheet.Cell(6, 2).Value = items.Count;
        sheet.Cell(7, 1).Value = "Teoría";
        sheet.Cell(7, 2).Value = theoryCount;
        sheet.Cell(8, 1).Value = "Manejo";
        sheet.Cell(8, 2).Value = practicalCount;
        sheet.Cell(9, 1).Value = "Exámenes";
        sheet.Cell(9, 2).Value = examCount;

        if (items.Count == 0)
        {
            sheet.Cell(11, 1).Value = "Sin programación para este día.";
            sheet.Cell(11, 1).Style.Font.Italic = true;
        }

        sheet.Columns().AdjustToContents(1, 40);
    }

    private static void WriteItems(IXLWorksheet sheet, IReadOnlyList<AgendaItemDto> items)
    {
        var headers = new[]
        {
            "Tipo", "Hora", "Fin", "Título", "Instructor", "Cupos", "Ocupados", "Disponibles", "Estudiantes", "Estado"
        };
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var item in items.OrderBy(x => x.StartTime, StringComparer.Ordinal).ThenBy(x => x.Title))
        {
            sheet.Cell(row, 1).Value = KindLabel(item.Kind);
            sheet.Cell(row, 2).Value = item.StartTime;
            sheet.Cell(row, 3).Value = item.EndTime ?? "";
            sheet.Cell(row, 4).Value = item.Title;
            sheet.Cell(row, 5).Value = item.InstructorName ?? "";
            sheet.Cell(row, 6).Value = item.Capacity;
            sheet.Cell(row, 7).Value = item.Occupied;
            sheet.Cell(row, 8).Value = item.Available;
            sheet.Cell(row, 9).Value = string.Join("; ", item.Students.Select(s => s.Name));
            sheet.Cell(row, 10).Value = item.Status;
            row++;
        }

        if (items.Count == 0)
        {
            sheet.Cell(2, 1).Value = "Sin ítems";
        }

        var lastRow = Math.Max(1, row - 1);
        sheet.Range(1, 1, lastRow, headers.Length).SetAutoFilter();
        sheet.Columns().AdjustToContents(1, 50);
    }

    private static string KindLabel(string kind) => kind switch
    {
        "theory" => "Teoría",
        "practical" or "driving" => "Manejo",
        "exam" or "theory-exam" => "Examen teórico",
        _ => kind
    };
}
