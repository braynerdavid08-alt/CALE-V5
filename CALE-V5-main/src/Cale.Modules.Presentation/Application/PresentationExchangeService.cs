using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Cale.Modules.Presentation.Application.Abstractions;
using Cale.Modules.Presentation.Application.DTOs;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Cale.Modules.Presentation.Application;

public sealed record ImportedSlideOutline(
    string Title,
    string Content,
    string? Notes,
    string? BackgroundJson = null,
    string? ElementsJson = null);

public sealed class PresentationExchangeService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<IReadOnlyList<ImportedSlideOutline>> ParseImportAsync(
        Stream stream,
        string fileName,
        IPresentationMediaStore mediaStore,
        int? ownerId,
        CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" or ".xls" => ParseExcel(stream),
            ".docx" => ParseWord(stream),
            ".pptx" => await PptxSlideIO.ImportAsync(stream, mediaStore, ownerId, ct),
            _ => throw new InvalidOperationException("Usa Excel (.xlsx), Word (.docx) o PowerPoint (.pptx).")
        };
    }

    public byte[] BuildExcelTemplate()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("Diapositivas");
        sheet.Cell(1, 1).Value = "#";
        sheet.Cell(1, 2).Value = "Título";
        sheet.Cell(1, 3).Value = "Contenido";
        sheet.Cell(1, 4).Value = "Notas del instructor";
        sheet.Cell(2, 1).Value = 1;
        sheet.Cell(2, 2).Value = "Señales reglamentarias";
        sheet.Cell(2, 3).Value = "• Qué son\n• Tipos principales\n• Ejemplos en vía";
        sheet.Cell(2, 4).Value = "Mencionar norma local";
        sheet.Cell(3, 1).Value = 2;
        sheet.Cell(3, 2).Value = "Señal PARE";
        sheet.Cell(3, 3).Value = "Significado: detenerse por completo.\nEjemplo: intersección sin semáforo.";
        sheet.Cell(3, 4).Value = "";
        sheet.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] BuildWordTemplate()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body());
            var body = main.Document.Body!;
            AppendWordHeading(body, "Plantilla Mi CALE — Presentaciones", 1);
            AppendWordParagraph(body, "Escribe cada diapositiva con un título (Estilo Título 1 o Título 2) y párrafos debajo para el contenido. Las notas del instructor van entre corchetes al final, por ejemplo [Nota: repasar examen].");
            AppendWordHeading(body, "Diapositiva 1 — Introducción", 2);
            AppendWordParagraph(body, "Bienvenida a la clase de hoy.");
            AppendWordParagraph(body, "Objetivos:\n• Reconocer señales\n• Aplicar normas básicas");
            AppendWordParagraph(body, "[Nota: saludar al grupo]");
            AppendWordHeading(body, "Diapositiva 2 — Señal PARE", 2);
            AppendWordParagraph(body, "Detenerse por completo antes de continuar.");
            main.Document.Save();
        }

        return ms.ToArray();
    }

    public byte[] ExportExcel(PresentationDetailDto detail)
    {
        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("Diapositivas");
        sheet.Cell(1, 1).Value = "Presentación";
        sheet.Cell(1, 2).Value = detail.Title;
        sheet.Cell(2, 1).Value = "Categoría";
        sheet.Cell(2, 2).Value = detail.Category;
        sheet.Cell(4, 1).Value = "#";
        sheet.Cell(4, 2).Value = "Título";
        sheet.Cell(4, 3).Value = "Contenido";
        sheet.Cell(4, 4).Value = "Notas del instructor";

        var row = 5;
        var index = 1;
        foreach (var slide in detail.Slides.OrderBy(s => s.Position))
        {
            sheet.Cell(row, 1).Value = index++;
            sheet.Cell(row, 2).Value = slide.Title;
            sheet.Cell(row, 3).Value = ExtractSlideBody(slide.ElementsJson);
            sheet.Cell(row, 4).Value = slide.Notes ?? "";
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportWord(PresentationDetailDto detail)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body());
            var body = main.Document.Body!;
            AppendWordHeading(body, detail.Title, 1);
            if (!string.IsNullOrWhiteSpace(detail.Description))
            {
                AppendWordParagraph(body, detail.Description);
            }

            foreach (var slide in detail.Slides.OrderBy(s => s.Position))
            {
                AppendWordHeading(body, slide.Title, 2);
                var content = ExtractSlideBody(slide.ElementsJson);
                foreach (var line in SplitLines(content))
                {
                    AppendWordParagraph(body, line);
                }

                if (!string.IsNullOrWhiteSpace(slide.Notes))
                {
                    AppendWordParagraph(body, $"[Nota del instructor: {slide.Notes}]", italic: true);
                }
            }

            main.Document.Save();
        }

        return ms.ToArray();
    }

    public byte[] ExportPowerPoint(PresentationDetailDto detail) => PptxSlideIO.Export(detail);

    public static (string SlideTitle, string BackgroundJson, string ElementsJson) BuildSlideFromOutline(
        ImportedSlideOutline outline)
    {
        var title = string.IsNullOrWhiteSpace(outline.Title) ? "Diapositiva" : outline.Title.Trim();
        var content = string.IsNullOrWhiteSpace(outline.Content) ? " " : outline.Content.Trim();
        var elements = new object[]
        {
            new
            {
                id = $"el-title-{Guid.NewGuid():N}"[..12],
                type = "text",
                x = 64,
                y = 48,
                w = 832,
                h = 64,
                rotation = 0,
                z = 1,
                props = new
                {
                    text = title,
                    fontSize = 36,
                    fontWeight = 700,
                    color = "#0B1F33",
                    align = "left",
                    fontFamily = "Segoe UI, sans-serif"
                }
            },
            new
            {
                id = $"el-body-{Guid.NewGuid():N}"[..12],
                type = "text",
                x = 64,
                y = 140,
                w = 832,
                h = 320,
                rotation = 0,
                z = 2,
                props = new
                {
                    text = content,
                    fontSize = 24,
                    fontWeight = 400,
                    color = "#243447",
                    align = "left",
                    fontFamily = "Segoe UI, sans-serif"
                }
            }
        };

        return (title, PresentationTemplates.LightBackground, JsonSerializer.Serialize(elements, JsonOptions));
    }

    private static IReadOnlyList<ImportedSlideOutline> ParseExcel(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var sheet = wb.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("El Excel no tiene hojas.");

        var used = sheet.RangeUsed();
        if (used is null)
        {
            throw new InvalidOperationException("El Excel está vacío.");
        }

        var headerRow = FindHeaderRow(sheet, used);
        var headers = ReadRow(sheet, headerRow);
        var titleCol = FindColumn(headers, "TITULO", "TÍTULO", "TITLE");
        var contentCol = FindColumn(headers, "CONTENIDO", "CONTENT", "TEXTO", "BODY");
        var notesCol = FindColumn(headers, "NOTAS", "NOTA", "NOTES", "NOTAS DEL INSTRUCTOR");

        if (titleCol < 0 && contentCol < 0)
        {
            titleCol = 2;
            contentCol = 3;
            notesCol = notesCol < 0 ? 4 : notesCol;
        }
        else
        {
            if (titleCol < 0)
            {
                titleCol = 2;
            }

            if (contentCol < 0)
            {
                contentCol = titleCol + 1;
            }

            if (notesCol < 0)
            {
                notesCol = contentCol + 1;
            }
        }

        var slides = new List<ImportedSlideOutline>();
        for (var row = headerRow + 1; row <= used.LastRow().RowNumber(); row++)
        {
            var title = GetCell(sheet, row, titleCol);
            var content = GetCell(sheet, row, contentCol);
            var notes = GetCell(sheet, row, notesCol);
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            slides.Add(new ImportedSlideOutline(
                string.IsNullOrWhiteSpace(title) ? $"Diapositiva {slides.Count + 1}" : title,
                content,
                string.IsNullOrWhiteSpace(notes) ? null : notes));
        }

        if (slides.Count == 0)
        {
            throw new InvalidOperationException("No se encontraron diapositivas en el Excel.");
        }

        return slides;
    }

    private static IReadOnlyList<ImportedSlideOutline> ParseWord(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("El documento Word está vacío.");

        var slides = new List<ImportedSlideOutline>();
        string? currentTitle = null;
        var content = new StringBuilder();
        string? notes = null;

        void Flush()
        {
            if (currentTitle is null && content.Length == 0)
            {
                return;
            }

            slides.Add(new ImportedSlideOutline(
                string.IsNullOrWhiteSpace(currentTitle) ? $"Diapositiva {slides.Count + 1}" : currentTitle,
                content.ToString().Trim(),
                notes));
            currentTitle = null;
            content.Clear();
            notes = null;
        }

        foreach (var element in body.Elements())
        {
            if (element is not W.Paragraph para)
            {
                continue;
            }

            var text = para.InnerText?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (TryExtractBracketNote(text, out var main, out var note))
            {
                if (!string.IsNullOrWhiteSpace(note))
                {
                    notes = string.IsNullOrWhiteSpace(notes) ? note : $"{notes}\n{note}";
                }

                text = main;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }
            }

            if (IsHeading(para))
            {
                Flush();
                currentTitle = text;
                continue;
            }

            if (currentTitle is null && slides.Count == 0 && content.Length == 0)
            {
                currentTitle = text;
                continue;
            }

            if (content.Length > 0)
            {
                content.Append('\n');
            }

            content.Append(text);
        }

        Flush();

        if (slides.Count == 0)
        {
            throw new InvalidOperationException(
                "No se encontraron diapositivas. Usa Título 1/Título 2 en Word o la plantilla de Mi CALE.");
        }

        return slides;
    }

    private static bool IsHeading(W.Paragraph para)
    {
        var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
        if (style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("Título", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("Titulo", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var outline = para.ParagraphProperties?.OutlineLevel?.Val?.Value;
        return outline is <= 2;
    }

    private static bool TryExtractBracketNote(string text, out string main, out string? note)
    {
        var match = Regex.Match(text, @"^(.*)\[(?:Nota|NOTE)[:\s]*(.*?)\]\s*$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            main = text;
            note = null;
            return false;
        }

        main = match.Groups[1].Value.Trim();
        note = match.Groups[2].Value.Trim();
        return true;
    }

    private static int FindHeaderRow(IXLWorksheet sheet, IXLRange used)
    {
        for (var row = used.FirstRow().RowNumber(); row <= Math.Min(used.LastRow().RowNumber(), 10); row++)
        {
            var cells = ReadRow(sheet, row);
            if (cells.Any(c => c.Contains("TITUL", StringComparison.OrdinalIgnoreCase)
                || c.Contains("CONTEN", StringComparison.OrdinalIgnoreCase)))
            {
                return row;
            }
        }

        return used.FirstRow().RowNumber();
    }

    private static List<string> ReadRow(IXLWorksheet sheet, int row)
    {
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 10;
        var cells = new List<string>();
        for (var col = 1; col <= lastCol; col++)
        {
            cells.Add(GetCell(sheet, row, col));
        }

        return cells;
    }

    private static int FindColumn(IReadOnlyList<string> headers, params string[] keys)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim().ToUpperInvariant();
            if (keys.Any(k => h.Contains(k, StringComparison.Ordinal)))
            {
                return i + 1;
            }
        }

        return -1;
    }

    private static string GetCell(IXLWorksheet sheet, int row, int col)
    {
        if (col <= 0)
        {
            return "";
        }

        return sheet.Cell(row, col).GetFormattedString().Trim();
    }

    private static string ExtractSlideBody(string elementsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(elementsJson);
            var sb = new StringBuilder();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!el.TryGetProperty("type", out var typeProp)
                    || typeProp.GetString() != "text")
                {
                    continue;
                }

                if (!el.TryGetProperty("props", out var props)
                    || !props.TryGetProperty("text", out var textProp))
                {
                    continue;
                }

                var text = textProp.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(text);
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return "";
        }
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return line;
        }
    }

    private static void AppendWordHeading(W.Body body, string text, int level)
    {
        var style = level <= 1 ? "Heading1" : "Heading2";
        var para = new W.Paragraph(
            new W.ParagraphProperties(new W.ParagraphStyleId { Val = style }),
            new W.Run(new W.Text(text)));
        body.Append(para);
    }

    private static void AppendWordParagraph(W.Body body, string text, bool italic = false)
    {
        var run = italic
            ? new W.Run(new W.RunProperties(new W.Italic()), new W.Text(text))
            : new W.Run(new W.Text(text));
        var para = new W.Paragraph(run);
        body.Append(para);
    }
}
