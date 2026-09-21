using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using V = DocumentFormat.OpenXml.Vml;

namespace Cale.Modules.Catalog.Application;

public sealed record ParsedExamImage(byte[] Data, string ContentType, string FileName);

public sealed record ParsedExamOption(
    string Letter,
    string Text,
    bool IsCorrect,
    ParsedExamImage? Image = null);

public sealed record ParsedExamQuestion(
    int Number,
    string Text,
    IReadOnlyList<ParsedExamOption> Options,
    bool NeedsCorrectReview,
    ParsedExamImage? Image = null);

public sealed record ParsedExamDocument(
    IReadOnlyList<ParsedExamQuestion> Questions,
    IReadOnlyList<string> Skipped,
    int MarkedCorrectCount,
    int ImagesFound = 0);

public sealed record ExamWordExportOption(char Letter, string Text, bool IsCorrect);

public sealed record ExamWordExportQuestion(
    int Number,
    string Text,
    IReadOnlyList<ExamWordExportOption> Options);

/// <summary>
/// Parses VIP-style theory exams from Word (.docx):
/// numbered stems (1. …) and A–D options, often concatenated on one line.
/// Mark the correct option with * before the letter (*B. …) or add a RESPUESTAS key.
/// Embedded images (DrawingML / VML) are attached to the current question or option.
/// </summary>
public static class ExamWordImportParser
{
    private const int MaxImageBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp"
    };

    private static readonly Regex QuestionStart = new(
        @"^\s*(\d{1,3})\s*[\.\)]\s+(.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OptionChunk = new(
        @"(?:^|[\s;])(\*?)\s*([A-Da-d])\s*\*?\s*[.\)\:\,\-–—]\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Recovers glued markers like "VerdeC) Amarillo" inside an option body.</summary>
    private static readonly Regex GluedOption = new(
        @"^(?<body>.+?)(?<star>\*?)\s*(?<letter>[A-Da-d])\s*\*?\s*[.\)\:\,\-–—]\s*(?<rest>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex AnswerKeyLine = new(
        @"(\d{1,3})\s*[\.\):\-]?\s*([A-Da-d])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private sealed record ParseUnit(string Text, IReadOnlyList<ParsedExamImage> Images);

    public static ParsedExamDocument Parse(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var main = doc.MainDocumentPart
            ?? throw new InvalidOperationException("El documento Word está vacío.");
        var body = main.Document?.Body
            ?? throw new InvalidOperationException("El documento Word está vacío.");

        var units = new List<ParseUnit>();
        var skippedMedia = new List<string>();
        foreach (var para in body.Elements<Paragraph>())
        {
            var text = Normalize(para.InnerText);
            var images = ExtractImages(para, main, skippedMedia);
            if (string.IsNullOrWhiteSpace(text) && images.Count == 0)
            {
                continue;
            }

            units.Add(new ParseUnit(text, images));
        }

        var parsed = ParseUnits(units);
        if (skippedMedia.Count == 0)
        {
            return parsed;
        }

        return parsed with
        {
            Skipped = parsed.Skipped.Concat(skippedMedia).ToList()
        };
    }

    public static ParsedExamDocument ParseLines(IReadOnlyList<string> lines) =>
        ParseUnits(lines.Select(l => new ParseUnit(l, Array.Empty<ParsedExamImage>())).ToList());

    private static ParsedExamDocument ParseUnits(IReadOnlyList<ParseUnit> units)
    {
        var answerKey = ExtractAnswerKey(units.Select(u => u.Text).ToList());
        var questions = new List<ParsedExamQuestion>();
        var skipped = new List<string>();
        var imagesFound = 0;

        int? currentNumber = null;
        var stem = new StringBuilder();
        var optionBuffer = new StringBuilder();
        ParsedExamImage? questionImage = null;
        var optionImages = new Dictionary<string, ParsedExamImage>(StringComparer.OrdinalIgnoreCase);
        string? lastOptionLetter = null;

        void TakeImages(IReadOnlyList<ParsedExamImage> images, string line, bool optionsStarted)
        {
            if (images.Count == 0 || currentNumber is null)
            {
                return;
            }

            imagesFound += images.Count;
            var remaining = images.ToList();

            if (optionsStarted)
            {
                var matches = string.IsNullOrWhiteSpace(line)
                    ? Array.Empty<Match>()
                    : OptionChunk.Matches(line).Cast<Match>().ToArray();

                if (matches.Length == 1)
                {
                    var letter = matches[0].Groups[2].Value.ToUpperInvariant();
                    if (!optionImages.ContainsKey(letter))
                    {
                        optionImages[letter] = remaining[0];
                        remaining.RemoveAt(0);
                    }

                    lastOptionLetter = letter;
                }
                else if (string.IsNullOrWhiteSpace(line)
                         && lastOptionLetter is not null
                         && !optionImages.ContainsKey(lastOptionLetter)
                         && remaining.Count > 0)
                {
                    // Image-only paragraph right under a single option line.
                    optionImages[lastOptionLetter] = remaining[0];
                    remaining.RemoveAt(0);
                }
            }

            foreach (var img in remaining)
            {
                if (questionImage is null)
                {
                    questionImage = img;
                }
                else
                {
                    skipped.Add(
                        $"Pregunta {currentNumber}: se omitió una imagen extra (solo se guarda una por enunciado).");
                }
            }
        }

        void Flush()
        {
            if (currentNumber is null)
            {
                return;
            }

            var options = SplitOptions(optionBuffer.ToString())
                .Select(o => optionImages.TryGetValue(o.Letter, out var img)
                    ? o with { Image = img }
                    : o)
                .ToList();
            var text = stem.ToString().Trim();
            if (string.IsNullOrWhiteSpace(text) || options.Count < 2)
            {
                skipped.Add(
                    $"Pregunta {currentNumber}: se omitió (faltan enunciado u opciones A–D).");
            }
            else
            {
                if (answerKey.TryGetValue(currentNumber.Value, out var correctLetter))
                {
                    options = options
                        .Select(o => o with
                        {
                            IsCorrect = string.Equals(
                                o.Letter,
                                correctLetter,
                                StringComparison.OrdinalIgnoreCase)
                        })
                        .ToList();
                }

                if (options.Count(o => o.IsCorrect) != 1)
                {
                    // Provisional for persistence (domain requires one correct).
                    // Live sessions exclude NeedsReview questions until the teacher fixes them.
                    options = options
                        .Select((o, i) => o with { IsCorrect = i == 0 })
                        .ToList();
                    questions.Add(new ParsedExamQuestion(
                        currentNumber.Value,
                        text,
                        options,
                        NeedsCorrectReview: true,
                        questionImage));
                }
                else
                {
                    questions.Add(new ParsedExamQuestion(
                        currentNumber.Value,
                        text,
                        options,
                        NeedsCorrectReview: false,
                        questionImage));
                }
            }

            currentNumber = null;
            stem.Clear();
            optionBuffer.Clear();
            questionImage = null;
            optionImages.Clear();
            lastOptionLetter = null;
        }

        foreach (var unit in units)
        {
            var line = unit.Text.Trim();
            if (IsAnswerKeyHeader(line))
            {
                Flush();
                break;
            }

            var qMatch = QuestionStart.Match(line);
            if (qMatch.Success)
            {
                Flush();
                currentNumber = int.Parse(qMatch.Groups[1].Value);
                var rest = qMatch.Groups[2].Value.Trim();
                if (LooksLikeOptions(rest))
                {
                    optionBuffer.Append(rest);
                    RememberLastOptionLetter(rest, ref lastOptionLetter);
                    TakeImages(unit.Images, rest, optionsStarted: true);
                }
                else
                {
                    stem.Append(rest);
                    TakeImages(unit.Images, rest, optionsStarted: false);
                }

                continue;
            }

            if (currentNumber is null)
            {
                continue;
            }

            if (LooksLikeOptions(line) || optionBuffer.Length > 0 && StartsWithOptionLetter(line))
            {
                if (optionBuffer.Length > 0)
                {
                    optionBuffer.Append(' ');
                }

                optionBuffer.Append(line);
                RememberLastOptionLetter(line, ref lastOptionLetter);
                TakeImages(unit.Images, line, optionsStarted: true);
            }
            else if (optionBuffer.Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    if (stem.Length > 0)
                    {
                        stem.Append(' ');
                    }

                    stem.Append(line);
                }

                TakeImages(unit.Images, line, optionsStarted: false);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    optionBuffer.Append(' ');
                    optionBuffer.Append(line);
                    RememberLastOptionLetter(line, ref lastOptionLetter);
                }

                TakeImages(unit.Images, line, optionsStarted: true);
            }
        }

        Flush();

        var marked = questions.Count(q => !q.NeedsCorrectReview);
        return new ParsedExamDocument(questions, skipped, marked, imagesFound);
    }

    public static byte[] BuildTemplateDocx()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(
                   ms,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                   true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(
                P("Plantilla de examen Mi CALE"),
                P("Puedes pegar una imagen debajo del enunciado; se vinculará a esa pregunta."),
                P("1. ¿Cuál es la respuesta correcta de ejemplo?"),
                P("*A. Opción correcta (marca con * la letra). B. Opción incorrecta. C. Otra incorrecta. D. Otra incorrecta."),
                P("2. Segunda pregunta de ejemplo:"),
                P("A. Primera"),
                P("*B. Correcta en línea aparte"),
                P("C. Tercera"),
                P("D. Cuarta"),
                P("RESPUESTAS (opcional): 1A 2B")));
            main.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] BuildExportDocx(
        string title,
        IReadOnlyList<ExamWordExportQuestion> questions)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(
                   ms,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                   true))
        {
            var body = new Body();
            body.AppendChild(P(string.IsNullOrWhiteSpace(title) ? "Examen Mi CALE" : title.Trim()));
            body.AppendChild(P(""));

            var keyParts = new List<string>();
            foreach (var q in questions)
            {
                body.AppendChild(P($"{q.Number}. {q.Text}"));
                var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                for (var i = 0; i < q.Options.Count; i++)
                {
                    var opt = q.Options[i];
                    var letter = i < letters.Length ? letters[i] : '?';
                    var mark = opt.IsCorrect ? "*" : "";
                    body.AppendChild(P($"{mark}{letter}. {opt.Text}"));
                    if (opt.IsCorrect)
                    {
                        keyParts.Add($"{q.Number}{letter}");
                    }
                }

                body.AppendChild(P(""));
            }

            if (keyParts.Count > 0)
            {
                body.AppendChild(P($"RESPUESTAS: {string.Join(" ", keyParts)}"));
            }

            var main = doc.AddMainDocumentPart();
            main.Document = new Document(body);
            main.Document.Save();
        }

        return ms.ToArray();
    }

    private static Paragraph P(string text) =>
        new(new Run(new Text(text)));

    private static void RememberLastOptionLetter(string line, ref string? lastOptionLetter)
    {
        var matches = OptionChunk.Matches(line);
        if (matches.Count > 0)
        {
            lastOptionLetter = matches[^1].Groups[2].Value.ToUpperInvariant();
        }
    }

    private static List<ParsedExamImage> ExtractImages(
        Paragraph para,
        MainDocumentPart main,
        List<string> skippedMedia)
    {
        var list = new List<ParsedExamImage>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var blip in para.Descendants<A.Blip>())
        {
            var embed = blip.Embed?.Value;
            if (string.IsNullOrWhiteSpace(embed) || !seen.Add(embed))
            {
                continue;
            }

            TryAddImagePart(main, embed, list, skippedMedia);
        }

        foreach (var imageData in para.Descendants<V.ImageData>())
        {
            var rel = imageData.RelationshipId?.Value;
            if (string.IsNullOrWhiteSpace(rel) || !seen.Add(rel))
            {
                continue;
            }

            TryAddImagePart(main, rel, list, skippedMedia);
        }

        return list;
    }

    private static void TryAddImagePart(
        MainDocumentPart main,
        string relationshipId,
        List<ParsedExamImage> list,
        List<string> skippedMedia)
    {
        OpenXmlPart? part;
        try
        {
            part = main.GetPartById(relationshipId);
        }
        catch
        {
            return;
        }

        if (part is not ImagePart imagePart)
        {
            return;
        }

        var contentType = (imagePart.ContentType ?? "").Trim();
        if (!AllowedImageTypes.Contains(contentType))
        {
            skippedMedia.Add(
                $"Se omitió una imagen embebida (formato no soportado: {contentType}).");
            return;
        }

        byte[] data;
        using (var input = imagePart.GetStream())
        using (var ms = new MemoryStream())
        {
            input.CopyTo(ms);
            data = ms.ToArray();
        }

        if (data.Length == 0)
        {
            return;
        }

        if (data.Length > MaxImageBytes)
        {
            skippedMedia.Add("Se omitió una imagen embebida (supera 5 MB).");
            return;
        }

        var ext = ExtForContentType(contentType);
        list.Add(new ParsedExamImage(data, NormalizeContentType(contentType), $"{Guid.NewGuid():N}{ext}"));
    }

    private static string ExtForContentType(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            _ => ".jpg"
        };

    private static string NormalizeContentType(string contentType) =>
        contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)
            ? "image/jpeg"
            : contentType;

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return Regex.Replace(text.Replace('\u00A0', ' '), @"\s+", " ").Trim();
    }

    private static bool IsAnswerKeyHeader(string line) =>
        line.StartsWith("RESPUESTA", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("CLAVES", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("HOJA DE RESPUESTAS", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeOptions(string text) =>
        OptionChunk.IsMatch(text) && OptionChunk.Matches(text).Count >= 1;

    private static bool StartsWithOptionLetter(string text) =>
        Regex.IsMatch(text, @"^\*?[A-Da-d]\s*[.\)\:\,\-–—]");

    private static List<ParsedExamOption> SplitOptions(string raw)
    {
        var text = raw.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // Normalize glued markers: "B)VerdeC)Amarillo" → "B) Verde C) Amarillo"
        text = Regex.Replace(
            text,
            @"(?<=\S)([A-Da-d])\s*([.\)\:\,\-–—])",
            " $1$2",
            RegexOptions.CultureInvariant);

        var matches = OptionChunk.Matches(text);
        if (matches.Count == 0)
        {
            return [];
        }

        var options = new List<ParsedExamOption>();
        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var start = m.Index + m.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            if (end < start)
            {
                continue;
            }

            var body = text[start..end].Trim().TrimEnd('.', ';', ',');
            body = Regex.Replace(body, @"\s*\(correcta\)\s*", " ", RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            var letter = m.Groups[2].Value.ToUpperInvariant();
            var starred = m.Groups[1].Value == "*"
                || body.Contains('✓')
                || body.Contains("correcta", StringComparison.OrdinalIgnoreCase);
            body = body.Replace("✓", "", StringComparison.Ordinal).Trim();

            // Split residual glued options that the primary pass still missed.
            while (true)
            {
                var glued = GluedOption.Match(body);
                if (!glued.Success)
                {
                    break;
                }

                var head = glued.Groups["body"].Value.Trim().TrimEnd('.', ';', ',');
                if (string.IsNullOrWhiteSpace(head))
                {
                    break;
                }

                options.Add(new ParsedExamOption(letter, head, starred));
                letter = glued.Groups["letter"].Value.ToUpperInvariant();
                starred = glued.Groups["star"].Value == "*";
                body = glued.Groups["rest"].Value.Trim().TrimEnd('.', ';', ',');
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            // Drop bodies that still look like mashed letters ("BCD") without real text.
            if (Regex.IsMatch(body, @"^[A-Da-d]{2,}$"))
            {
                continue;
            }

            options.Add(new ParsedExamOption(letter, body, starred));
        }

        // Deduplicate by letter keeping first.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return options.Where(o => seen.Add(o.Letter)).ToList();
    }

    private static Dictionary<int, string> ExtractAnswerKey(IReadOnlyList<string> lines)
    {
        var map = new Dictionary<int, string>();
        var inKey = false;
        foreach (var line in lines)
        {
            if (IsAnswerKeyHeader(line))
            {
                inKey = true;
            }

            if (!inKey && !line.Contains("RESPUESTA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            inKey = true;
            foreach (Match m in AnswerKeyLine.Matches(line))
            {
                map[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value.ToUpperInvariant();
            }
        }

        return map;
    }
}
