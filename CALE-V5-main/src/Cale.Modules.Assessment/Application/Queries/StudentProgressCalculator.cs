using System.Globalization;
using Cale.BuildingBlocks.Domain.Scoring;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;

namespace Cale.Modules.Assessment.Application.Queries;

/// <summary>
/// Builds the progress dashboard from finished attempts. Pure: every number comes from the rows.
/// Trend = average of the last k results − average of the k before them, with k = min(5, n / 2),
/// in percentage points; |trend| &lt; <see cref="FlatTrendPoints"/> is reported as "flat".
/// </summary>
public static class StudentProgressCalculator
{
    public const int TrendWindowMax = 5;
    public const decimal FlatTrendPoints = 2m;
    public const int MaxWeeks = 8;

    public const string TrendUp = "up";
    public const string TrendDown = "down";
    public const string TrendFlat = "flat";
    public const string TrendNone = "none";

    /// <summary>Colombia has no daylight saving time.</summary>
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    /// <param name="rows">Finished attempts of the selected mode, oldest first.</param>
    /// <param name="totalForMode">Finished attempts of the mode in the database (rows may be capped).</param>
    public static StudentProgressDto Build(
        IReadOnlyList<AttemptProgressRow> rows,
        int totalForMode,
        string mode,
        int? take,
        ProgressModeCountsDto counts)
    {
        var offset = Math.Max(0, totalForMode - rows.Count);
        var numbered = rows
            .Select((row, index) => MapAttempt(row, offset + index + 1))
            .ToList();
        var selected = take is > 0 && numbered.Count > take.Value
            ? numbered.Skip(numbered.Count - take.Value).ToList()
            : numbered;

        var scores = selected.Select(x => x.Score).ToList();
        var durations = selected
            .Where(x => x.DurationSeconds is not null)
            .Select(x => x.DurationSeconds!.Value)
            .ToList();

        var window = Math.Min(TrendWindowMax, scores.Count / 2);
        decimal? recent = null;
        decimal? previous = null;
        decimal? trend = null;
        var direction = TrendNone;
        if (window >= 1)
        {
            recent = Average(scores.Skip(scores.Count - window));
            previous = Average(scores.Skip(scores.Count - 2 * window).Take(window));
            trend = Math.Round(recent!.Value - previous!.Value, 1);
            direction = trend >= FlatTrendPoints
                ? TrendUp
                : trend <= -FlatTrendPoints
                    ? TrendDown
                    : TrendFlat;
        }

        var thresholds = selected.Select(x => x.PassThreshold).Distinct().ToList();

        return new StudentProgressDto(
            mode,
            take is > 0 ? take : null,
            counts,
            selected.Count,
            selected.Count(x => x.Passed),
            Average(scores),
            scores.Count == 0 ? null : scores.Max(),
            scores.Count == 0 ? null : scores[^1],
            Average(scores.Skip(Math.Max(0, scores.Count - TrendWindowMax))),
            window,
            recent,
            previous,
            trend,
            direction,
            durations.Count == 0 ? null : durations.Min(),
            durations.Count == 0 ? null : (int)Math.Round(durations.Average()),
            selected.Count == 0 ? null : selected[^1].DurationSeconds,
            selected.Count - durations.Count,
            thresholds.Count == 1 ? thresholds[0] : null,
            ScoringRules.MaxIncorrectAnswers,
            selected,
            BuildWeeks(selected));
    }

    public static DateOnly ColombiaDate(DateTime utc) =>
        DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(ColombiaOffset));

    private static ProgressAttemptDto MapAttempt(AttemptProgressRow row, int number) => new(
        number,
        row.Id,
        row.Mode,
        DateTime.SpecifyKind(row.FinishedAt, DateTimeKind.Utc),
        Math.Round(row.Percent, 1),
        row.CorrectCount,
        row.TotalQuestions,
        row.Passed,
        BandOf(row),
        ScoringRules.PassThresholdPercent(row.TotalQuestions),
        row.TimeSeconds > 0 ? row.TimeSeconds : null);

    /// <summary>The stored Passed flag is the source of truth; the band only refines failed attempts.</summary>
    private static string BandOf(AttemptProgressRow row)
    {
        if (row.Passed)
        {
            return ScoringRules.BandPassed;
        }

        var band = ScoringRules.ResultBand(row.CorrectCount, row.TotalQuestions);
        return band == ScoringRules.BandPassed ? ScoringRules.BandNear : band;
    }

    private static IReadOnlyList<ProgressWeekDto> BuildWeeks(IReadOnlyList<ProgressAttemptDto> attempts) =>
        attempts
            .GroupBy(x => MondayOf(ColombiaDate(x.FinishedAt)))
            .OrderBy(g => g.Key)
            .TakeLast(MaxWeeks)
            .Select(g =>
            {
                var items = g.ToList();
                return new ProgressWeekDto(
                    g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    g.Key.AddDays(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    items.Count,
                    items[0].Score,
                    items[^1].Score,
                    items.Max(x => x.Score),
                    items.Min(x => x.Score),
                    Average(items.Select(x => x.Score))!.Value);
            })
            .ToList();

    private static DateOnly MondayOf(DateOnly date)
    {
        var shift = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-shift);
    }

    private static decimal? Average(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(), 1);
    }
}
