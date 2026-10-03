using System.Text.Json;
using System.Text.Json.Nodes;
using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Courses.Application;

/// <summary>
/// Lesson content is a JSON array of blocks. Supported types:
/// text, tip, image, video, audio, signs, quiz, flipcards, match.
/// Quiz answers never leave the server; students check them one by one and the
/// final score is computed here.
/// </summary>
public static class CourseContent
{
    public const int MaxJsonChars = 400_000;
    public const int MaxBlocks = 60;
    private const int MaxText = 6000;
    private const int MaxShort = 400;

    public static readonly string[] BlockTypes = ["text", "tip", "image", "video", "audio", "signs", "quiz", "flipcards", "match"];

    /// <summary>Validates and normalizes editor content; returns compact JSON.</summary>
    public static string Normalize(JsonElement? content)
    {
        if (content is null || content.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "[]";
        }

        var raw = content.Value.GetRawText();
        if (raw.Length > MaxJsonChars)
        {
            throw Invalid("El contenido de la lección es demasiado grande. Divídelo en dos lecciones.");
        }

        if (JsonNode.Parse(raw) is not JsonArray blocks)
        {
            throw Invalid("El contenido de la lección no es válido.");
        }

        if (blocks.Count > MaxBlocks)
        {
            throw Invalid($"Una lección puede tener máximo {MaxBlocks} bloques.");
        }

        var result = new JsonArray();
        for (var i = 0; i < blocks.Count; i++)
        {
            result.Add(NormalizeBlock(blocks[i] as JsonObject, i + 1));
        }

        return result.ToJsonString();
    }

    public static JsonArray Parse(string json) =>
        JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json) as JsonArray ?? new JsonArray();

    /// <summary>Copy of the content without quiz answers or explanations.</summary>
    public static JsonArray ForStudent(string json)
    {
        var blocks = Parse(json);
        foreach (var block in blocks.OfType<JsonObject>())
        {
            if (Type(block) == "quiz")
            {
                block.Remove("correct");
                block.Remove("explanation");
            }
        }

        return blocks;
    }

    public static int QuizCount(string json) => Parse(json).OfType<JsonObject>().Count(b => Type(b) == "quiz");

    public static (bool Correct, int CorrectIndex, string? Explanation) Check(string json, int blockIndex, int option)
    {
        var quiz = QuizAt(Parse(json), blockIndex);
        var correct = quiz["correct"]!.GetValue<int>();
        return (option == correct, correct, quiz["explanation"]?.GetValue<string>());
    }

    /// <summary>Percentage of quiz blocks answered correctly; 100 when the lesson has no quiz.</summary>
    public static int Score(string json, IReadOnlyDictionary<int, int>? answers)
    {
        var blocks = Parse(json);
        var total = 0;
        var right = 0;
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is not JsonObject block || Type(block) != "quiz")
            {
                continue;
            }

            total++;
            if (answers is not null
                && answers.TryGetValue(i, out var chosen)
                && chosen == block["correct"]!.GetValue<int>())
            {
                right++;
            }
        }

        return total == 0 ? 100 : (int)Math.Round(right * 100.0 / total);
    }

    private static JsonObject QuizAt(JsonArray blocks, int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= blocks.Count || blocks[blockIndex] is not JsonObject block || Type(block) != "quiz")
        {
            throw new DomainException("Esa pregunta no existe.", 404, "course_quiz_not_found");
        }

        return block;
    }

    private static string Type(JsonObject block) => block["type"]?.GetValue<string>() ?? "";

    private static JsonObject NormalizeBlock(JsonObject? block, int n)
    {
        if (block is null)
        {
            throw Invalid($"El bloque {n} no es válido.");
        }

        var type = Str(block, "type", 20)?.ToLowerInvariant() ?? "";
        return type switch
        {
            "text" => new JsonObject
            {
                ["type"] = type,
                ["title"] = Str(block, "title", MaxShort),
                ["body"] = Required(block, "body", MaxText, $"Escribe el texto del bloque {n}.")
            },
            "tip" => new JsonObject
            {
                ["type"] = type,
                ["body"] = Required(block, "body", MaxText, $"Escribe el texto del recuadro del bloque {n}.")
            },
            "image" or "video" or "audio" => new JsonObject
            {
                ["type"] = type,
                ["url"] = Url(block, n),
                ["caption"] = Str(block, "caption", MaxShort)
            },
            "signs" => NormalizeSigns(block, n),
            "quiz" => NormalizeQuiz(block, n),
            "flipcards" => NormalizeFlipcards(block, n),
            "match" => NormalizeMatch(block, n),
            _ => throw Invalid($"El bloque {n} tiene un tipo desconocido.")
        };
    }

    private static JsonObject NormalizeSigns(JsonObject block, int n)
    {
        var items = new JsonArray();
        foreach (var item in Items(block, "items", 1, 24, n, "señales"))
        {
            var code = Required(item, "code", 20, $"Falta el código de una señal en el bloque {n}.");
            items.Add(new JsonObject
            {
                ["code"] = code,
                ["name"] = Required(item, "name", MaxShort, $"Falta el nombre de la señal {code}."),
                ["note"] = Str(item, "note", MaxShort)
            });
        }

        return new JsonObject { ["type"] = "signs", ["title"] = Str(block, "title", MaxShort), ["items"] = items };
    }

    private static JsonObject NormalizeQuiz(JsonObject block, int n)
    {
        var question = Required(block, "question", MaxShort * 2, $"Escribe la pregunta del bloque {n}.");
        if (block["options"] is not JsonArray rawOptions)
        {
            throw Invalid($"La pregunta del bloque {n} necesita opciones.");
        }

        var options = new JsonArray();
        foreach (var o in rawOptions)
        {
            var text = (o?.GetValueKind() == JsonValueKind.String ? o.GetValue<string>() : "").Trim();
            if (text.Length == 0)
            {
                throw Invalid($"Hay una opción vacía en la pregunta del bloque {n}.");
            }

            options.Add(text.Length > MaxShort ? text[..MaxShort] : text);
        }

        if (options.Count is < 2 or > 6)
        {
            throw Invalid($"La pregunta del bloque {n} debe tener entre 2 y 6 opciones.");
        }

        var correct = Int(block, "correct");
        if (correct is null || correct < 0 || correct >= options.Count)
        {
            throw Invalid($"Marca la respuesta correcta de la pregunta del bloque {n}.");
        }

        return new JsonObject
        {
            ["type"] = "quiz",
            ["question"] = question,
            ["imageUrl"] = OptionalUrl(block, "imageUrl"),
            ["options"] = options,
            ["correct"] = correct,
            ["explanation"] = Str(block, "explanation", MaxText)
        };
    }

    private static JsonObject NormalizeFlipcards(JsonObject block, int n)
    {
        var cards = new JsonArray();
        foreach (var card in Items(block, "cards", 1, 20, n, "tarjetas"))
        {
            cards.Add(new JsonObject
            {
                ["front"] = Required(card, "front", MaxShort, $"Falta el frente de una tarjeta en el bloque {n}."),
                ["back"] = Required(card, "back", MaxShort * 2, $"Falta el reverso de una tarjeta en el bloque {n}."),
                ["imageUrl"] = OptionalUrl(card, "imageUrl")
            });
        }

        return new JsonObject { ["type"] = "flipcards", ["title"] = Str(block, "title", MaxShort), ["cards"] = cards };
    }

    private static JsonObject NormalizeMatch(JsonObject block, int n)
    {
        var pairs = new JsonArray();
        foreach (var pair in Items(block, "pairs", 2, 10, n, "parejas"))
        {
            var imageUrl = OptionalUrl(pair, "imageUrl");
            var left = Str(pair, "left", MaxShort);
            if (left is null && imageUrl is null)
            {
                throw Invalid($"Cada pareja del bloque {n} necesita un texto o una imagen a la izquierda.");
            }

            pairs.Add(new JsonObject
            {
                ["left"] = left,
                ["imageUrl"] = imageUrl,
                ["right"] = Required(pair, "right", MaxShort, $"Falta la respuesta de una pareja en el bloque {n}.")
            });
        }

        return new JsonObject
        {
            ["type"] = "match",
            ["instructions"] = Str(block, "instructions", MaxShort),
            ["pairs"] = pairs
        };
    }

    private static IEnumerable<JsonObject> Items(JsonObject block, string key, int min, int max, int n, string label)
    {
        var items = (block[key] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        if (items.Count < min || items.Count > max)
        {
            throw Invalid($"El bloque {n} debe tener entre {min} y {max} {label}.");
        }

        return items;
    }

    private static string Url(JsonObject block, int n) =>
        OptionalUrl(block, "url") ?? throw Invalid($"Sube el archivo o pega el enlace del bloque {n}.");

    private static string? OptionalUrl(JsonObject block, string key)
    {
        var url = Str(block, key, 500);
        if (url is null)
        {
            return null;
        }

        var ok = url.StartsWith('/')
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        if (!ok)
        {
            throw Invalid("Los enlaces deben empezar por https://.");
        }

        return url;
    }

    private static string Required(JsonObject block, string key, int max, string message) =>
        Str(block, key, max) ?? throw Invalid(message);

    private static string? Str(JsonObject block, string key, int max)
    {
        var node = block[key];
        if (node is null || node.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }

        var v = node.GetValue<string>().Trim();
        if (v.Length == 0)
        {
            return null;
        }

        return v.Length > max ? v[..max] : v;
    }

    private static int? Int(JsonObject block, string key)
    {
        var node = block[key];
        return node?.GetValueKind() == JsonValueKind.Number && node.AsValue().TryGetValue(out int value) ? value : null;
    }

    private static DomainException Invalid(string message) => new(message, 400, "course_content_invalid");
}
