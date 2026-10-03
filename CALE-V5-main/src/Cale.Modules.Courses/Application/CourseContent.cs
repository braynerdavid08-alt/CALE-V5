using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Courses.Application;

/// <summary>
/// Lesson content is a JSON array of blocks. Content blocks: text, tip, image, video, audio, signs,
/// flipcards, match, hotspot. Graded activities: quiz, truefalse, scenario, order, fillblank, classify.
/// Answers of graded activities never leave the server; students check them one by one and the
/// final score is computed here.
/// </summary>
public static partial class CourseContent
{
    public const int MaxJsonChars = 400_000;
    public const int MaxBlocks = 60;
    private const int MaxText = 6000;
    private const int MaxShort = 400;

    public static readonly string[] BlockTypes =
    [
        "text", "tip", "image", "video", "audio", "signs", "quiz", "flipcards", "match",
        "truefalse", "order", "hotspot", "scenario", "fillblank", "classify"
    ];

    private static readonly HashSet<string> Graded = ["quiz", "truefalse", "scenario", "order", "fillblank", "classify"];

    [GeneratedRegex(@"\[\[(.+?)\]\]")]
    private static partial Regex BlankPattern();

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

    /// <summary>Copy of the content without answers; activity items are shuffled the same way every time.</summary>
    public static JsonArray ForStudent(string json)
    {
        var blocks = Parse(json);
        var result = new JsonArray();
        for (var i = 0; i < blocks.Count; i++)
        {
            result.Add(blocks[i] is JsonObject block ? StudentBlock(block, i) : null);
        }

        return result;
    }

    /// <summary>Number of graded activities in the lesson.</summary>
    public static int QuizCount(string json) => Parse(json).OfType<JsonObject>().Count(b => Graded.Contains(Type(b)));

    public static (bool Correct, int CorrectIndex, string? Explanation, JsonNode? Solution) Check(
        string json,
        int blockIndex,
        int option,
        JsonElement? answer = null)
    {
        var blocks = Parse(json);
        if (blockIndex < 0 || blockIndex >= blocks.Count || blocks[blockIndex] is not JsonObject block || !Graded.Contains(Type(block)))
        {
            throw new DomainException("Esa actividad no existe.", 404, "course_quiz_not_found");
        }

        var value = answer is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } a
            ? a
            : JsonSerializer.SerializeToElement(option);
        return Grade(block, blockIndex, value);
    }

    /// <summary>Percentage of graded activities answered correctly; 100 when the lesson has none.</summary>
    public static int Score(string json, IReadOnlyDictionary<int, JsonElement>? answers)
    {
        var blocks = Parse(json);
        var total = 0;
        var right = 0;
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is not JsonObject block || !Graded.Contains(Type(block)))
            {
                continue;
            }

            total++;
            if (answers is not null && answers.TryGetValue(i, out var answer) && Grade(block, i, answer).Correct)
            {
                right++;
            }
        }

        return total == 0 ? 100 : (int)Math.Round(right * 100.0 / total);
    }

    // ── Grading ─────────────────────────────────────────────────────────

    private static (bool Correct, int CorrectIndex, string? Explanation, JsonNode? Solution) Grade(
        JsonObject block,
        int index,
        JsonElement answer)
    {
        var explanation = block["explanation"]?.GetValue<string>();
        switch (Type(block))
        {
            case "quiz":
            {
                var correct = block["correct"]!.GetValue<int>();
                return (AsInt(answer) == correct, correct, explanation, null);
            }
            case "truefalse":
            {
                var correct = block["answer"]!.GetValue<bool>() ? 0 : 1;
                return (AsInt(answer) == correct, correct, explanation, null);
            }
            case "scenario":
            {
                var choices = (JsonArray)block["choices"]!;
                var best = choices.Select((c, i) => (c, i)).First(x => x.c!["best"]!.GetValue<bool>()).i;
                var chosen = AsInt(answer);
                var outcomes = new JsonArray(choices.Select(c => (JsonNode?)JsonValue.Create(c!["outcome"]!.GetValue<string>())).ToArray());
                var ok = chosen >= 0 && chosen < choices.Count && choices[chosen]!["best"]!.GetValue<bool>();
                var said = chosen >= 0 && chosen < choices.Count ? choices[chosen]!["outcome"]!.GetValue<string>() : null;
                return (ok, best, said, outcomes);
            }
            case "order":
            {
                var steps = Strings((JsonArray)block["steps"]!);
                var given = AsStrings(answer);
                var ok = given.Count == steps.Count && steps.Zip(given).All(p => Same(p.First, p.Second));
                return (ok, -1, explanation, new JsonArray(steps.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()));
            }
            case "fillblank":
            {
                var blanks = Blanks(block["text"]!.GetValue<string>());
                var given = AsStrings(answer);
                var ok = given.Count == blanks.Count
                    && blanks.Zip(given).All(p => p.First.Any(alt => Same(alt, p.Second)));
                return (ok, -1, explanation, new JsonArray(blanks.Select(b => (JsonNode?)JsonValue.Create(b[0])).ToArray()));
            }
            case "classify":
            {
                var items = ((JsonArray)block["items"]!).OfType<JsonObject>().ToList();
                var order = ShuffledOrder(items.Count, Seed(block, index), avoidIdentity: false);
                var expected = order.Select(o => items[o]["group"]!.GetValue<int>()).ToList();
                var given = AsInts(answer);
                var ok = given.Count == expected.Count && expected.SequenceEqual(given);
                return (ok, -1, explanation, new JsonArray(expected.Select(g => (JsonNode?)JsonValue.Create(g)).ToArray()));
            }
            default:
                return (false, -1, null, null);
        }
    }

    private static JsonObject StudentBlock(JsonObject source, int index)
    {
        var block = (JsonObject)source.DeepClone();
        switch (Type(block))
        {
            case "quiz":
                block.Remove("correct");
                block.Remove("explanation");
                break;
            case "truefalse":
                block.Remove("answer");
                block.Remove("explanation");
                break;
            case "scenario":
                block["choices"] = new JsonArray(((JsonArray)source["choices"]!)
                    .Select(c => (JsonNode?)new JsonObject { ["text"] = c!["text"]!.GetValue<string>() })
                    .ToArray());
                break;
            case "order":
            {
                var steps = Strings((JsonArray)source["steps"]!);
                var order = ShuffledOrder(steps.Count, Seed(source, index), avoidIdentity: true);
                block["steps"] = new JsonArray(order.Select(o => (JsonNode?)JsonValue.Create(steps[o])).ToArray());
                block.Remove("explanation");
                break;
            }
            case "fillblank":
            {
                var text = source["text"]!.GetValue<string>();
                var blanks = Blanks(text);
                var bank = blanks.Select(b => b[0])
                    .Concat(Strings(source["distractors"] as JsonArray ?? []))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var bankOrder = ShuffledOrder(bank.Count, Seed(source, index), avoidIdentity: true);
                block.Remove("text");
                block.Remove("distractors");
                block.Remove("explanation");
                block["parts"] = new JsonArray(BlankPattern().Split(text)
                    .Where((_, i) => i % 2 == 0)
                    .Select(p => (JsonNode?)JsonValue.Create(p))
                    .ToArray());
                block["bank"] = new JsonArray(bankOrder.Select(o => (JsonNode?)JsonValue.Create(bank[o])).ToArray());
                break;
            }
            case "classify":
            {
                var items = ((JsonArray)source["items"]!).OfType<JsonObject>().ToList();
                var order = ShuffledOrder(items.Count, Seed(source, index), avoidIdentity: false);
                block["items"] = new JsonArray(order
                    .Select(o => (JsonNode?)new JsonObject
                    {
                        ["text"] = items[o]["text"]?.GetValue<string>(),
                        ["imageUrl"] = items[o]["imageUrl"]?.GetValue<string>()
                    })
                    .ToArray());
                block.Remove("explanation");
                break;
            }
        }

        return block;
    }

    private static List<List<string>> Blanks(string text) =>
        BlankPattern().Matches(text)
            .Select(m => m.Groups[1].Value.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToList())
            .ToList();

    private static int Seed(JsonObject block, int index)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in block.ToJsonString())
            {
                hash = (hash ^ ch) * 16777619u;
            }

            return (int)(hash ^ (uint)index);
        }
    }

    private static List<int> ShuffledOrder(int count, int seed, bool avoidIdentity)
    {
        var order = Enumerable.Range(0, count).ToList();
        var random = new Random(seed);
        for (var i = count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        if (avoidIdentity && count > 1 && order.Select((v, i) => v == i).All(x => x))
        {
            order.Add(order[0]);
            order.RemoveAt(0);
        }

        return order;
    }

    private static bool Same(string a, string b) => Fold(a) == Fold(b);

    private static string Fold(string value)
    {
        var decomposed = (value ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    private static int AsInt(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var v) ? v : -1;

    private static List<string> AsStrings(JsonElement e) =>
        e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "").ToList()
            : [];

    private static List<int> AsInts(JsonElement e) =>
        e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Select(AsInt).ToList() : [];

    private static List<string> Strings(JsonArray array) =>
        array.Select(x => x?.GetValueKind() == JsonValueKind.String ? x.GetValue<string>() : "").ToList();

    private static string Type(JsonObject block) => block["type"]?.GetValue<string>() ?? "";

    // ── Normalization ───────────────────────────────────────────────────

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
            "truefalse" => NormalizeTrueFalse(block, n),
            "order" => NormalizeOrder(block, n),
            "hotspot" => NormalizeHotspot(block, n),
            "scenario" => NormalizeScenario(block, n),
            "fillblank" => NormalizeFillBlank(block, n),
            "classify" => NormalizeClassify(block, n),
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
        var options = TextList(block, "options", 2, 6, n, "opciones", $"Hay una opción vacía en la pregunta del bloque {n}.");
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

    private static JsonObject NormalizeTrueFalse(JsonObject block, int n)
    {
        var answer = block["answer"];
        if (answer is null || answer.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw Invalid($"Marca si la afirmación del bloque {n} es verdadera o falsa.");
        }

        return new JsonObject
        {
            ["type"] = "truefalse",
            ["statement"] = Required(block, "statement", MaxShort * 2, $"Escribe la afirmación del bloque {n}."),
            ["imageUrl"] = OptionalUrl(block, "imageUrl"),
            ["answer"] = answer.GetValue<bool>(),
            ["explanation"] = Str(block, "explanation", MaxText)
        };
    }

    private static JsonObject NormalizeOrder(JsonObject block, int n) => new()
    {
        ["type"] = "order",
        ["instructions"] = Str(block, "instructions", MaxShort),
        ["steps"] = TextList(block, "steps", 3, 8, n, "pasos", $"Hay un paso vacío en el bloque {n}."),
        ["explanation"] = Str(block, "explanation", MaxText)
    };

    private static JsonObject NormalizeHotspot(JsonObject block, int n)
    {
        var imageUrl = OptionalUrl(block, "imageUrl") ?? throw Invalid($"Sube la imagen del bloque {n}.");
        var spots = new JsonArray();
        foreach (var spot in Items(block, "spots", 1, 8, n, "puntos"))
        {
            spots.Add(new JsonObject
            {
                ["x"] = Percent(spot, "x", n),
                ["y"] = Percent(spot, "y", n),
                ["label"] = Required(spot, "label", MaxShort, $"Escribe qué es cada punto del bloque {n}."),
                ["note"] = Str(spot, "note", MaxShort * 2)
            });
        }

        return new JsonObject
        {
            ["type"] = "hotspot",
            ["instructions"] = Str(block, "instructions", MaxShort),
            ["imageUrl"] = imageUrl,
            ["spots"] = spots
        };
    }

    private static JsonObject NormalizeScenario(JsonObject block, int n)
    {
        var choices = new JsonArray();
        var anyBest = false;
        foreach (var choice in Items(block, "choices", 2, 4, n, "decisiones"))
        {
            var best = choice["best"]?.GetValueKind() == JsonValueKind.True;
            anyBest |= best;
            choices.Add(new JsonObject
            {
                ["text"] = Required(choice, "text", MaxShort, $"Escribe cada decisión del bloque {n}."),
                ["outcome"] = Required(choice, "outcome", MaxShort * 2, $"Escribe qué pasa con cada decisión del bloque {n}."),
                ["best"] = best
            });
        }

        if (!anyBest)
        {
            throw Invalid($"Marca la mejor decisión de la situación del bloque {n}.");
        }

        return new JsonObject
        {
            ["type"] = "scenario",
            ["situation"] = Required(block, "situation", MaxShort * 3, $"Describe la situación del bloque {n}."),
            ["imageUrl"] = OptionalUrl(block, "imageUrl"),
            ["choices"] = choices
        };
    }

    private static JsonObject NormalizeFillBlank(JsonObject block, int n)
    {
        var text = Required(block, "text", MaxShort * 3, $"Escribe la frase del bloque {n}.");
        var blanks = Blanks(text);
        if (blanks.Count is < 1 or > 8 || blanks.Any(b => b.Count == 0))
        {
            throw Invalid($"En el bloque {n} marca entre 1 y 8 espacios con doble corchete, por ejemplo [[50]].");
        }

        var distractors = new JsonArray();
        if (block["distractors"] is JsonArray raw)
        {
            foreach (var d in Strings(raw).Select(s => s.Trim()).Where(s => s.Length > 0).Take(6))
            {
                distractors.Add(d.Length > MaxShort ? d[..MaxShort] : d);
            }
        }

        return new JsonObject
        {
            ["type"] = "fillblank",
            ["text"] = text,
            ["distractors"] = distractors,
            ["explanation"] = Str(block, "explanation", MaxText)
        };
    }

    private static JsonObject NormalizeClassify(JsonObject block, int n)
    {
        var groups = TextList(block, "groups", 2, 4, n, "grupos", $"Hay un grupo sin nombre en el bloque {n}.");
        var items = new JsonArray();
        foreach (var item in Items(block, "items", 2, 16, n, "elementos"))
        {
            var text = Str(item, "text", MaxShort);
            var imageUrl = OptionalUrl(item, "imageUrl");
            if (text is null && imageUrl is null)
            {
                throw Invalid($"Cada elemento del bloque {n} necesita un texto o una imagen.");
            }

            var group = Int(item, "group");
            if (group is null || group < 0 || group >= groups.Count)
            {
                throw Invalid($"Elige el grupo correcto de cada elemento del bloque {n}.");
            }

            items.Add(new JsonObject { ["text"] = text, ["imageUrl"] = imageUrl, ["group"] = group });
        }

        return new JsonObject
        {
            ["type"] = "classify",
            ["instructions"] = Str(block, "instructions", MaxShort),
            ["groups"] = groups,
            ["items"] = items,
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

    private static JsonArray TextList(JsonObject block, string key, int min, int max, int n, string label, string emptyMessage)
    {
        if (block[key] is not JsonArray raw)
        {
            throw Invalid($"El bloque {n} necesita {label}.");
        }

        var result = new JsonArray();
        foreach (var o in raw)
        {
            var text = (o?.GetValueKind() == JsonValueKind.String ? o.GetValue<string>() : "").Trim();
            if (text.Length == 0)
            {
                throw Invalid(emptyMessage);
            }

            result.Add(text.Length > MaxShort ? text[..MaxShort] : text);
        }

        if (result.Count < min || result.Count > max)
        {
            throw Invalid($"El bloque {n} debe tener entre {min} y {max} {label}.");
        }

        return result;
    }

    private static double Percent(JsonObject item, string key, int n)
    {
        var node = item[key];
        if (node?.GetValueKind() != JsonValueKind.Number || !node.AsValue().TryGetValue(out double v) || v is < 0 or > 100)
        {
            throw Invalid($"Hay un punto fuera de la imagen en el bloque {n}.");
        }

        return Math.Round(v, 2);
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
