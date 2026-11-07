namespace Cale.Api.Services.Play;

/// <summary>Versioned weights for the general competitive index (V1).</summary>
public static class RankingWeightsV1
{
    public const string Version = "v1";
    public const double Xp = 0.50;
    public const double Trophies = 0.30;
    public const double Achievements = 0.20;

    /// <summary>
    /// Average percentile rank (0–100). Ties share the average rank of their block.
    /// Higher raw value → higher percentile.
    /// </summary>
    public static double Percentile(int indexZeroBased, int count, IReadOnlyList<int> sortedDescendingValues)
    {
        if (count <= 1)
        {
            return 100;
        }

        var value = sortedDescendingValues[indexZeroBased];
        var first = indexZeroBased;
        while (first > 0 && sortedDescendingValues[first - 1] == value)
        {
            first--;
        }

        var last = indexZeroBased;
        while (last + 1 < count && sortedDescendingValues[last + 1] == value)
        {
            last++;
        }

        var avgRank = (first + last) / 2.0; // 0 = best
        return 100.0 * (1.0 - avgRank / (count - 1));
    }

    public static double GeneralIndex(double xpPct, double trophyPct, double achievementPct) =>
        xpPct * Xp + trophyPct * Trophies + achievementPct * Achievements;
}
