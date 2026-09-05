using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Cale.Modules.Catalog.Application;

public sealed record ParsedExamOption(string Letter, string Text, bool IsCorrect);

public sealed record ParsedExamQuestion(
    int Number,
    string Text,
    IReadOnlyList<ParsedExamOption> Options,
    bool NeedsCorrectReview);

public sealed record ParsedExamDocument(
    IReadOnlyList<ParsedExamQuestion> Questions,
    IReadOnlyList<string> Skipped,
    int MarkedCorrectCount);

/// <summary>
/// Parses VIP-style theory exams from Word (.docx):
/// numbered stems (1. …) and A–D options, often concatenated on one line.
/// Mark the correct option with * before the letter (*B. …) or add a RESPUESTAS key.
/// </summary>
public static class ExamWordImportParser
{
    private static readonly Regex QuestionStart = new(
        @"^\s*(\d{1,3})\s*[\.\)]\s+(.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OptionChunk = new(
        @"(?:^|\s)(\*?)\s*([A-Da-d])\s*\*?\s*[\.\)\:]\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AnswerKeyLine = new(
        @"(\d{1,3})\s*[\.\):\-]?\s*([A-Da-d])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ParsedExamDocument Parse(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("El documento Word está vacío.");

        var lines = new List<string>();
        foreach (var para in body.Elements<Paragraph>())
        {
            var text = Normalize(para.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }
        }

        return ParseLines(lines);
    }

    public static ParsedExamDocument ParseLines(IReadOnlyList<string> lines)
    {
        var answerKey = ExtractAnswerKey(lines);
        var questions = new List<ParsedExamQuestion>();
        var skipped = new List<string>();

        int? currentNumber = null;
        var stem = new StringBuilder();
        var optionBuffer = new StringBuilder();

        void Flush()
        {
            if (currentNumber is null)
            {
                return;
            }

            var options = SplitOptions(optionBuffer.ToString());
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
                    // Provisional: keep first option correct but flag for teacher review.
                    options = options
                        .Select((o, i) => o with { IsCorrect = i == 0 })
                        .ToList();
                    questions.Add(new ParsedExamQuestion(
                        currentNumber.Value,
                        text,
                        options,
                        NeedsCorrectReview: true));
                }
                else
                {
                    questions.Add(new ParsedExamQuestion(
                        currentNumber.Value,
                        text,
                        options,
                        NeedsCorrectReview: false));
                }
            }

            currentNumber = null;
            stem.Clear();
            optionBuffer.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
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
                }
                else
                {
                    stem.Append(rest);
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
            }
            else if (optionBuffer.Length == 0)
            {
                if (stem.Length > 0)
                {
                    stem.Append(' ');
                }

                stem.Append(line);
            }
            else
            {
                optionBuffer.Append(' ');
                optionBuffer.Append(line);
            }
        }

        Flush();

        var marked = questions.Count(q => !q.NeedsCorrectReview);
        return new ParsedExamDocument(questions, skipped, marked);
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

    private static Paragraph P(string text) =>
        new(new Run(new Text(text)));

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
        Regex.IsMatch(text, @"^\*?[A-Da-d]\s*[\.\)\:]");

    private static List<ParsedExamOption> SplitOptions(string raw)
    {
        var text = raw.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

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
