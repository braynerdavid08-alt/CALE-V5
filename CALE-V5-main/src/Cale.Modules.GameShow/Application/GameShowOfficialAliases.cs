using System.Text.Json;
using Cale.Modules.GameShow.Application.DTOs;

namespace Cale.Modules.GameShow.Application;

/// <summary>
/// Admin-approved aliases for the official pack, keyed by normalized question → normalized answer.
/// The pack itself is a read-only file, so these live in the DB and are merged on read.
/// </summary>
public static class GameShowOfficialAliases
{
    public static Dictionary<string, Dictionary<string, List<string>>> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(json) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public static string Serialize(Dictionary<string, Dictionary<string, List<string>>> map) =>
        JsonSerializer.Serialize(map);

    public static void Add(
        Dictionary<string, Dictionary<string, List<string>>> map,
        string questionText,
        string answerText,
        string alias)
    {
        var q = GameShowAnswerMatcher.Normalize(questionText);
        var a = GameShowAnswerMatcher.Normalize(answerText);
        if (!map.TryGetValue(q, out var answers))
        {
            answers = new();
            map[q] = answers;
        }

        if (!answers.TryGetValue(a, out var list))
        {
            list = new();
            answers[a] = list;
        }

        if (!list.Contains(alias, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(alias);
        }
    }

    public static CreateGameShowRequest Merge(CreateGameShowRequest body, string? json)
    {
        var map = Parse(json);
        if (map.Count == 0)
        {
            return body;
        }

        return body with
        {
            Rounds = body.Rounds
                .Select(r =>
                {
                    if (!map.TryGetValue(GameShowAnswerMatcher.Normalize(r.QuestionText), out var answers))
                    {
                        return r;
                    }

                    return r with
                    {
                        Answers = r.Answers
                            .Select(a => answers.TryGetValue(GameShowAnswerMatcher.Normalize(a.Text), out var extra)
                                ? a with { Aliases = MergeAliases(a.Aliases, extra) }
                                : a)
                            .ToList()
                    };
                })
                .ToList()
        };
    }

    public static bool ContainsQuestion(CreateGameShowRequest? body, string questionText)
    {
        if (body is null)
        {
            return false;
        }

        var key = GameShowAnswerMatcher.Normalize(questionText);
        return body.Rounds.Any(r => GameShowAnswerMatcher.Normalize(r.QuestionText) == key);
    }

    /// <summary>Adds an alias to the matching round/answer of a pack body; null when not found.</summary>
    public static CreateGameShowRequest? AddToBody(
        CreateGameShowRequest body,
        string questionText,
        string answerText,
        string alias)
    {
        var q = GameShowAnswerMatcher.Normalize(questionText);
        var a = GameShowAnswerMatcher.Normalize(answerText);
        var found = false;
        var rounds = body.Rounds
            .Select(r =>
            {
                if (GameShowAnswerMatcher.Normalize(r.QuestionText) != q)
                {
                    return r;
                }

                return r with
                {
                    Answers = r.Answers
                        .Select(ans =>
                        {
                            if (GameShowAnswerMatcher.Normalize(ans.Text) != a)
                            {
                                return ans;
                            }

                            found = true;
                            return ans with { Aliases = MergeAliases(ans.Aliases, [alias]) };
                        })
                        .ToList()
                };
            })
            .ToList();
        return found ? body with { Rounds = rounds } : null;
    }

    private static IReadOnlyList<string> MergeAliases(IReadOnlyList<string>? current, IEnumerable<string> extra) =>
        (current ?? [])
            .Concat(extra)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(GameShowAnswerMatcher.MaxAliases)
            .ToList();
}
