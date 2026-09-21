using System.Globalization;
using System.Text;
using System.Text.Json;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.GameShow.Application.DTOs;

namespace Cale.Modules.GameShow.Application;

/// <summary>
/// Parses GameShow question packs exported as JSON or CSV into a create request.
/// </summary>
public static class GameShowQuestionImport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static CreateGameShowRequest Parse(string text, string? fileName)
    {
        var raw = StripBom((text ?? "").Trim());
        if (raw.Length == 0)
        {
            throw new DomainException("El archivo está vacío.", 400, "empty_import");
        }

        var lower = (fileName ?? "").Trim().ToLowerInvariant();
        if (lower.EndsWith(".json", StringComparison.Ordinal)
            || raw.StartsWith('{')
            || raw.StartsWith('['))
        {
            return ParseJson(raw);
        }

        if (lower.EndsWith(".csv", StringComparison.Ordinal) || LooksLikeCsv(raw))
        {
            return ParseCsv(raw);
        }

        throw new DomainException(
            "Usa un archivo .json o .csv exportado desde CALE.",
            400,
            "unsupported_import");
    }

    public static CreateGameShowRequest ParseJson(string raw)
    {
        CreateGameShowRequest? body;
        try
        {
            body = JsonSerializer.Deserialize<CreateGameShowRequest>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            throw new DomainException("JSON inválido.", 400, "invalid_json");
        }

        if (body?.Rounds is null || body.Rounds.Count < 1)
        {
            throw new DomainException("El JSON no tiene rondas.", 400, "invalid_rounds");
        }

        if (body.Rounds.Count > 20)
        {
            throw new DomainException("Máximo 20 rondas por partida.", 400, "too_many_rounds");
        }

        var rounds = body.Rounds.Select(NormalizeRound).ToList();
        return new CreateGameShowRequest(
            string.IsNullOrWhiteSpace(body.Title) ? "100 Estudiantes Dijeron" : body.Title.Trim(),
            string.IsNullOrWhiteSpace(body.TeamAName) ? "Equipo A" : body.TeamAName.Trim(),
            string.IsNullOrWhiteSpace(body.TeamBName) ? "Equipo B" : body.TeamBName.Trim(),
            rounds);
    }

    public static CreateGameShowRequest ParseCsv(string raw)
    {
        var rows = ParseCsvRows(raw);
        if (rows.Count < 2)
        {
            throw new DomainException("El CSV no tiene filas de datos.", 400, "invalid_csv");
        }

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        var iRonda = Col(header, "ronda", "round");
        var iPregunta = Col(header, "pregunta", "question", "questiontext");
        var iRank = Col(header, "rank", "orden", "posicion", "posición");
        var iRespuesta = Col(header, "respuesta", "answer", "text");
        var iPuntos = Col(header, "puntos", "points", "score");
        var iAliases = Col(header, "aliases", "alias", "sinonimos", "sinónimos");

        if (iRonda < 0 || iPregunta < 0 || iRespuesta < 0 || iPuntos < 0)
        {
            throw new DomainException(
                "Cabecera CSV inválida. Esperado: ronda,pregunta,rank,respuesta,puntos,aliases",
                400,
                "invalid_csv_header");
        }

        var byRound = new SortedDictionary<int, RoundAcc>();
        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (!TryInt(Cell(row, iRonda), out var roundNo) || roundNo < 1)
            {
                throw new DomainException($"Fila {r + 1}: número de ronda inválido.", 400, "invalid_csv");
            }

            var question = Cell(row, iPregunta).Trim();
            var answerText = Cell(row, iRespuesta).Trim();
            if (!TryInt(Cell(row, iPuntos), out var points) || points < 1)
            {
                points = 1;
            }

            var fallbackRank = byRound.TryGetValue(roundNo, out var existing)
                ? existing.ByRank.Count + 1
                : 1;
            var rank = iRank >= 0 && TryInt(Cell(row, iRank), out var parsedRank) && parsedRank > 0
                ? parsedRank
                : fallbackRank;

            var aliases = iAliases >= 0
                ? Cell(row, iAliases)
                    .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToList()
                : [];

            if (!byRound.TryGetValue(roundNo, out var acc))
            {
                acc = new RoundAcc { QuestionText = question };
                byRound[roundNo] = acc;
            }
            else if (string.IsNullOrWhiteSpace(acc.QuestionText) && question.Length > 0)
            {
                acc.QuestionText = question;
            }

            acc.ByRank[rank] = new CreateGameShowAnswerRequest(answerText, points, aliases);
        }

        if (byRound.Count < 1)
        {
            throw new DomainException("No se encontraron rondas en el CSV.", 400, "invalid_csv");
        }

        if (byRound.Count > 20)
        {
            throw new DomainException("Máximo 20 rondas por partida.", 400, "too_many_rounds");
        }

        var rounds = byRound.Values.Select(acc =>
        {
            var answers = acc.ByRank.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
            while (answers.Count < 5)
            {
                answers.Add(new CreateGameShowAnswerRequest("", Math.Max(1, 30 - answers.Count * 5), []));
            }

            return new CreateGameShowRoundRequest(acc.QuestionText, null, answers.Take(5).ToList());
        }).ToList();

        return new CreateGameShowRequest(
            "100 Estudiantes Dijeron",
            "Equipo A",
            "Equipo B",
            rounds);
    }

    private static CreateGameShowRoundRequest NormalizeRound(CreateGameShowRoundRequest round)
    {
        var answers = (round.Answers ?? [])
            .Select(a => new CreateGameShowAnswerRequest(
                (a.Text ?? "").Trim(),
                a.Points < 1 ? 1 : a.Points,
                a.Aliases?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList()))
            .ToList();

        while (answers.Count < 5)
        {
            answers.Add(new CreateGameShowAnswerRequest("", Math.Max(1, 30 - answers.Count * 5), []));
        }

        return new CreateGameShowRoundRequest(
            (round.QuestionText ?? "").Trim(),
            round.SourceQuestionId,
            answers.Take(5).ToList());
    }

    private static bool LooksLikeCsv(string raw)
    {
        var firstLine = raw.Split('\n', 2)[0].Trim().TrimStart('\uFEFF').ToLowerInvariant();
        return firstLine.Contains("ronda", StringComparison.Ordinal)
            && firstLine.Contains("pregunta", StringComparison.Ordinal)
            && firstLine.Contains("respuesta", StringComparison.Ordinal);
    }

    private static string StripBom(string s) =>
        s.Length > 0 && s[0] == '\uFEFF' ? s[1..] : s;

    private static int Col(IReadOnlyList<string> header, params string[] names)
    {
        foreach (var name in names)
        {
            for (var i = 0; i < header.Count; i++)
            {
                if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string Cell(IReadOnlyList<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index] : "";

    private static bool TryInt(string value, out int n) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n);

    private static List<List<string>> ParseCsvRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\n':
                    row.Add(cell.ToString());
                    rows.Add(row);
                    row = [];
                    cell.Clear();
                    break;
                case '\r':
                    break;
                default:
                    cell.Append(ch);
                    break;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private sealed class RoundAcc
    {
        public string QuestionText { get; set; } = "";
        public SortedDictionary<int, CreateGameShowAnswerRequest> ByRank { get; } = new();
    }
}
