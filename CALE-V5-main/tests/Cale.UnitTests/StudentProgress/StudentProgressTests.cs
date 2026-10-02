using Cale.BuildingBlocks.Domain.Assessment;
using Cale.BuildingBlocks.Domain.Scoring;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Assessment.Application.Queries;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Assessment.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cale.UnitTests.StudentProgress;

public sealed class StudentProgressCalculatorTests
{
    private static readonly DateTime Base = new(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc); // Monday 09:00 Colombia
    private static readonly ProgressModeCountsDto Counts = new(0, 0, 0);

    /// <summary>40-question attempt with <paramref name="correct"/> right answers, one per day.</summary>
    private static AttemptProgressRow Row(int id, int correct, int day, int seconds = 600, int total = 40, string mode = AttemptModes.Exam)
    {
        var finished = Base.AddDays(day);
        return new AttemptProgressRow(
            id,
            mode,
            total,
            correct,
            Math.Round(100m * correct / total, 2),
            ScoringRules.IsPassed(correct, total),
            seconds,
            finished.AddSeconds(-seconds),
            finished);
    }

    private static StudentProgressDto Build(IReadOnlyList<AttemptProgressRow> rows, int? take = null) =>
        StudentProgressCalculator.Build(rows, rows.Count, "all", take, Counts);

    [Fact]
    public void No_attempts_returns_empty_dashboard_without_invented_values()
    {
        var dto = Build([]);

        Assert.Equal(0, dto.TotalAttempts);
        Assert.Null(dto.AverageScore);
        Assert.Null(dto.BestScore);
        Assert.Null(dto.LastScore);
        Assert.Null(dto.Trend);
        Assert.Equal(StudentProgressCalculator.TrendNone, dto.TrendDirection);
        Assert.Null(dto.ApprovalThreshold);
        Assert.Empty(dto.Attempts);
        Assert.Empty(dto.Weeks);
    }

    [Fact]
    public void One_attempt_has_no_trend_and_a_single_point_week()
    {
        var dto = Build([Row(1, 30, 0)]);

        Assert.Equal(1, dto.TotalAttempts);
        Assert.Equal(75m, dto.AverageScore);
        Assert.Equal(75m, dto.BestScore);
        Assert.Equal(75m, dto.LastScore);
        Assert.Equal(StudentProgressCalculator.TrendNone, dto.TrendDirection);
        var week = Assert.Single(dto.Weeks);
        Assert.Equal(1, week.Count);
        Assert.Equal(week.Open, week.Close);
        Assert.Equal(week.High, week.Low);
    }

    [Fact]
    public void Average_best_last_and_passed_count_come_from_the_rows()
    {
        // 50%, 100%, 92.5% (passed: 3 wrong), 85% (6 wrong → near), 0%
        var dto = Build([Row(1, 20, 0), Row(2, 40, 1), Row(3, 37, 2), Row(4, 34, 3), Row(5, 0, 4)]);

        Assert.Equal(5, dto.TotalAttempts);
        Assert.Equal(65.5m, dto.AverageScore);
        Assert.Equal(100m, dto.BestScore);
        Assert.Equal(0m, dto.LastScore);
        Assert.Equal(2, dto.PassedAttempts);
        Assert.Equal(
            ["failed", "passed", "passed", "near", "failed"],
            dto.Attempts.Select(a => a.Band).ToArray());
    }

    [Fact]
    public void All_passed_and_all_failed()
    {
        var passed = Build([Row(1, 40, 0), Row(2, 38, 1), Row(3, 37, 2)]);
        Assert.Equal(3, passed.PassedAttempts);
        Assert.All(passed.Attempts, a => Assert.True(a.Passed));

        var failed = Build([Row(1, 10, 0), Row(2, 20, 1), Row(3, 36, 2)]);
        Assert.Equal(0, failed.PassedAttempts);
        Assert.All(failed.Attempts, a => Assert.False(a.Passed));
    }

    [Fact]
    public void Approval_line_uses_the_real_rule_of_max_three_wrong_answers()
    {
        var dto = Build([Row(1, 30, 0), Row(2, 35, 1)]);

        Assert.Equal(92.5m, dto.ApprovalThreshold);
        Assert.Equal(ScoringRules.MaxIncorrectAnswers, dto.MaxIncorrectAnswers);
        Assert.All(dto.Attempts, a => Assert.Equal(92.5m, a.PassThreshold));
    }

    [Fact]
    public void Mixed_question_counts_have_no_single_approval_line_but_each_point_keeps_its_own()
    {
        var dto = Build([Row(1, 18, 0, total: 20), Row(2, 37, 1, total: 40)]);

        Assert.Null(dto.ApprovalThreshold);
        Assert.Equal([85m, 92.5m], dto.Attempts.Select(a => a.PassThreshold).ToArray());
    }

    [Fact]
    public void Trend_compares_last_five_against_previous_five()
    {
        // previous five: 50% each; last five: 75% each → +25 points
        var rows = Enumerable.Range(0, 5).Select(i => Row(i + 1, 20, i))
            .Concat(Enumerable.Range(5, 5).Select(i => Row(i + 1, 30, i)))
            .ToList();

        var dto = Build(rows);

        Assert.Equal(5, dto.TrendWindow);
        Assert.Equal(75m, dto.RecentAverage);
        Assert.Equal(50m, dto.PreviousAverage);
        Assert.Equal(25m, dto.Trend);
        Assert.Equal(StudentProgressCalculator.TrendUp, dto.TrendDirection);
        Assert.Equal(75m, dto.LastFiveAverage);
    }

    [Fact]
    public void Trend_with_few_attempts_uses_half_and_reports_flat_or_down()
    {
        var flat = Build([Row(1, 30, 0), Row(2, 30, 1), Row(3, 30, 2)]);
        Assert.Equal(1, flat.TrendWindow);
        Assert.Equal(0m, flat.Trend);
        Assert.Equal(StudentProgressCalculator.TrendFlat, flat.TrendDirection);

        var down = Build([Row(1, 40, 0), Row(2, 20, 1)]);
        Assert.Equal(-50m, down.Trend);
        Assert.Equal(StudentProgressCalculator.TrendDown, down.TrendDirection);
    }

    [Fact]
    public void Attempts_without_duration_are_excluded_from_time_metrics_only()
    {
        var dto = Build([Row(1, 30, 0, seconds: 0), Row(2, 40, 1, seconds: 448), Row(3, 20, 2, seconds: 1800)]);

        Assert.Equal(3, dto.TotalAttempts);
        Assert.Null(dto.Attempts[0].DurationSeconds);
        Assert.Equal(1, dto.AttemptsWithoutDuration);
        Assert.Equal(448, dto.BestDurationSeconds);
        Assert.Equal(1124, dto.AverageDurationSeconds);
        Assert.Equal(1800, dto.LastDurationSeconds);
    }

    [Fact]
    public void Take_filter_keeps_chronological_numbers_and_recomputes_stats()
    {
        var rows = Enumerable.Range(0, 12).Select(i => Row(i + 1, 20 + i, i)).ToList();

        var dto = Build(rows, take: 5);

        Assert.Equal(5, dto.Take);
        Assert.Equal(5, dto.TotalAttempts);
        Assert.Equal([8, 9, 10, 11, 12], dto.Attempts.Select(a => a.Number).ToArray());
        Assert.Equal(Math.Round(100m * 31 / 40, 1), dto.LastScore);
    }

    [Fact]
    public void Numbers_continue_when_older_rows_were_capped()
    {
        var rows = new[] { Row(50, 30, 0), Row(51, 31, 1) };

        var dto = StudentProgressCalculator.Build(rows, totalForMode: 120, "all", null, Counts);

        Assert.Equal([119, 120], dto.Attempts.Select(a => a.Number).ToArray());
    }

    [Fact]
    public void Weekly_candles_use_open_close_high_low_in_colombia_weeks()
    {
        // Week of 7 Sep: 50%, 90%, 10%, 60% → open 50, close 60, high 90, low 10.
        // Sunday 13 Sep 23:30 Colombia (= Monday 04:30 UTC) still belongs to that week.
        var sundayLate = new DateTime(2026, 9, 14, 4, 30, 0, DateTimeKind.Utc);
        var rows = new List<AttemptProgressRow>
        {
            Row(1, 20, 0), Row(2, 36, 1), Row(3, 4, 2),
            new(4, AttemptModes.Exam, 40, 24, 60m, false, 600, sundayLate.AddMinutes(-10), sundayLate),
            Row(5, 40, 8)
        };

        var dto = Build(rows);

        Assert.Equal(2, dto.Weeks.Count);
        var first = dto.Weeks[0];
        Assert.Equal("2026-09-07", first.WeekStart);
        Assert.Equal("2026-09-13", first.WeekEnd);
        Assert.Equal(4, first.Count);
        Assert.Equal(50m, first.Open);
        Assert.Equal(60m, first.Close);
        Assert.Equal(90m, first.High);
        Assert.Equal(10m, first.Low);
        Assert.Equal("2026-09-14", dto.Weeks[1].WeekStart);
    }

    [Fact]
    public void Stored_failed_flag_is_never_shown_as_passed()
    {
        var legacy = new AttemptProgressRow(1, AttemptModes.Exam, 40, 38, 95m, false, 300, Base, Base.AddMinutes(5));

        var dto = Build([legacy]);

        Assert.False(dto.Attempts[0].Passed);
        Assert.Equal(ScoringRules.BandNear, dto.Attempts[0].Band);
        Assert.Equal(0, dto.PassedAttempts);
    }
}

/// <summary>Store query + handler on a real SQLite database.</summary>
public sealed class StudentProgressQueryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cale-progress-{Guid.NewGuid():N}.db");
    private static readonly DateTime T0 = new(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc);

    public StudentProgressQueryTests()
    {
        using var db = NewDb();
        db.Database.EnsureCreated();
    }

    private CaleDbContext NewDb() => new(
        new DbContextOptionsBuilder<CaleDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=False")
            .ReplaceService<IModelCacheKeyFactory, AssessmentOnlyModelKey>()
            .Options,
        new MappingAssemblies(typeof(AttemptConfiguration).Assembly));

    /// <summary>EF caches one model per context type; other fixtures map different assemblies on the same type.</summary>
    private sealed class AssessmentOnlyModelKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => (context.GetType(), nameof(AssessmentOnlyModelKey), designTime);
    }

    private void Seed(params Attempt[] attempts)
    {
        using var db = NewDb();
        db.Set<Attempt>().AddRange(attempts);
        db.SaveChanges();
    }

    private static Attempt Finished(int user, string mode, int correct, DateTime started, int minutes = 10, int total = 40)
    {
        var attempt = Attempt.Start(user, 1, mode == AttemptModes.Exam ? 1 : null, mode, total, 60, started);
        attempt.Finish(correct, started.AddMinutes(minutes));
        return attempt;
    }

    private async Task<StudentProgressDto> Progress(int user, string? mode = null, int? take = null)
    {
        await using var db = NewDb();
        return await new StudentProgressHandler(new AttemptStore(db)).HandleAsync(user, mode, take, CancellationToken.None);
    }

    [Fact]
    public async Task Orders_by_finish_time_not_by_id_and_ignores_open_attempts_and_other_users()
    {
        var late = Finished(1, AttemptModes.Exam, 40, T0.AddDays(3));
        var early = Finished(1, AttemptModes.Exam, 20, T0);
        var open = Attempt.Start(1, 1, null, AttemptModes.Practice, 40, 60, T0.AddDays(4));
        var other = Finished(2, AttemptModes.Exam, 10, T0.AddDays(1));
        Seed(late, early, open, other);

        var dto = await Progress(1);

        Assert.Equal(2, dto.TotalAttempts);
        Assert.Equal([early.Id, late.Id], dto.Attempts.Select(a => a.AttemptId).ToArray());
        Assert.Equal([1, 2], dto.Attempts.Select(a => a.Number).ToArray());
        Assert.Equal(100m, dto.LastScore);
        Assert.Equal(600, dto.Attempts[0].DurationSeconds);
    }

    [Fact]
    public async Task Exam_and_practice_modes_are_kept_apart()
    {
        Seed(
            Finished(1, AttemptModes.Exam, 40, T0),
            Finished(1, AttemptModes.Practice, 10, T0.AddDays(1)),
            Finished(1, AttemptModes.MixedPractice, 20, T0.AddDays(2)));

        var exam = await Progress(1, "exam");
        var practice = await Progress(1, "practice");
        var all = await Progress(1, "anything-else");

        Assert.Equal(new ProgressModeCountsDto(3, 1, 2), all.ModeCounts);
        Assert.Equal("all", all.Mode);
        Assert.Equal(3, all.TotalAttempts);
        Assert.Equal(1, exam.TotalAttempts);
        Assert.Equal(100m, exam.AverageScore);
        Assert.Equal(2, practice.TotalAttempts);
        Assert.Equal(37.5m, practice.AverageScore);
    }

    [Fact]
    public async Task New_finished_attempt_appears_on_next_load()
    {
        Seed(Finished(1, AttemptModes.Exam, 20, T0));
        Assert.Equal(1, (await Progress(1)).TotalAttempts);

        Seed(Finished(1, AttemptModes.Exam, 40, T0.AddDays(1)));
        var after = await Progress(1);

        Assert.Equal(2, after.TotalAttempts);
        Assert.Equal(100m, after.LastScore);
        Assert.Equal(1, after.PassedAttempts);
    }

    [Fact]
    public async Task Unsupported_take_values_mean_all()
    {
        Seed(Enumerable.Range(0, 7).Select(i => Finished(1, AttemptModes.Exam, 30, T0.AddDays(i))).ToArray());

        Assert.Equal(7, (await Progress(1, take: 3)).TotalAttempts);
        Assert.Equal(5, (await Progress(1, take: 5)).TotalAttempts);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }
}
