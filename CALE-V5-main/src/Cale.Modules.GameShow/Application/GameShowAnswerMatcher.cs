using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cale.Modules.GameShow.Application;

public static class GameShowAnswerMatcher
{
    private static readonly Regex NonLetters = new(@"[^\p{L}\p{N}\s]", RegexOptions.Compiled);

    public static bool Matches(string submitted, string canonical, string aliasesJson)
    {
        var norm = Normalize(submitted);
        if (string.IsNullOrEmpty(norm))
        {
            return false;
        }

        if (norm == Normalize(canonical))
        {
            return true;
        }

        foreach (var alias in ParseAliases(aliasesJson))
        {
            var aliasNorm = Normalize(alias);
            if (norm == aliasNorm)
            {
                return true;
            }

            if (SoftContains(norm, aliasNorm))
            {
                return true;
            }
        }

        return SoftContains(norm, Normalize(canonical));
    }

    /// <summary>
    /// Soft match when one normalized phrase contains the other.
    /// Requires target length ≥ 5 and limited padding so vague related words do not match.
    /// </summary>
    private static bool SoftContains(string submitted, string target)
    {
        if (string.IsNullOrEmpty(submitted) || string.IsNullOrEmpty(target))
        {
            return false;
        }

        if (submitted.Contains(target) && target.Length >= 5)
        {
            // "revisar las luces" contains "luces"; reject huge unrelated sentences.
            return submitted.Length <= target.Length + 24;
        }

        if (target.Contains(submitted) && submitted.Length >= 5)
        {
            return target.Length <= submitted.Length + 24;
        }

        return false;
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var form = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(form.Length);
        foreach (var ch in form)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(ch);
        }

        var cleaned = NonLetters.Replace(sb.ToString().Normalize(NormalizationForm.FormC), " ");
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    public static string SerializeAliases(IEnumerable<string>? aliases)
    {
        var list = (aliases ?? [])
            .Select(a => a.Trim())
            .Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
        return JsonSerializer.Serialize(list);
    }

    public static IReadOnlyList<string> ParseAliases(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
