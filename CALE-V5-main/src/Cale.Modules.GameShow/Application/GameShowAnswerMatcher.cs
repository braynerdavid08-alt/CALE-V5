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
            if (norm == Normalize(alias))
            {
                return true;
            }
        }

        // Soft containment only when both sides are short phrases and one contains the other.
        var canon = Normalize(canonical);
        if (canon.Length >= 4 && (norm.Contains(canon) || canon.Contains(norm)))
        {
            var shorter = Math.Min(norm.Length, canon.Length);
            var longer = Math.Max(norm.Length, canon.Length);
            if (shorter >= 4 && longer <= shorter + 12)
            {
                return true;
            }
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

/// <summary>Centralized scoring rules for steal / round end.</summary>
public static class GameShowScoringPolicy
{
    public const int MaxStrikes = 3;

    /// <summary>
    /// On successful steal, the stealing team receives the points of the newly revealed answer
    /// plus the points already banked for the controlling team this round.
    /// </summary>
    public static int ComputeSuccessfulStealPoints(
        int newlyRevealedPoints,
        int controllerBankedPoints) =>
        newlyRevealedPoints + controllerBankedPoints;

    public static string OppositeTeam(string team) =>
        string.Equals(team, Domain.GameShowTeams.A, StringComparison.OrdinalIgnoreCase)
            ? Domain.GameShowTeams.B
            : Domain.GameShowTeams.A;
}
