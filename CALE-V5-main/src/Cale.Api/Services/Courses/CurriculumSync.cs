using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Courses;

/// <summary>
/// Brings the platform courses in line with <see cref="CourseSeed.Curriculum"/>, matching lessons by their stable
/// seed key instead of their position. A lesson whose content differs from what the seed wrote is treated as
/// edited by hand and is never changed or removed. School courses are never read or written.
/// </summary>
public sealed class CurriculumSync
{
    private static readonly HashSet<string> InteractiveTypes =
        ["quiz", "truefalse", "scenario", "fillblank", "order", "classify", "match", "hotspot"];

    private readonly CaleDbContext _db;
    private readonly ILogger<CurriculumSync> _logger;

    public CurriculumSync(CaleDbContext db, ILogger<CurriculumSync> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<CurriculumPlanDto> PlanAsync(CancellationToken ct) => RunAsync(false, false, ct);

    public async Task<CurriculumPlanDto> ApplyAsync(bool resetProgress, CancellationToken ct)
    {
        var result = await RunAsync(true, resetProgress, ct);
        _logger.LogWarning(
            "Curriculum applied: {Before} -> {After} platform lessons, {Removed} progress rows removed (reset={Reset}).",
            result.LessonsBefore,
            result.LessonsAfter,
            result.ProgressRowsRemoved,
            resetProgress);
        return result;
    }

    private async Task<CurriculumPlanDto> RunAsync(bool apply, bool resetProgress, CancellationToken ct)
    {
        var curriculum = CourseSeed.Curriculum();
        var titles = curriculum.SelectMany(c => c.Lessons).ToDictionary(l => l.Key, l => l.Title);
        var platform = await _db.Set<Course>().Where(c => c.SchoolUserId == null && c.Slug != null).ToListAsync(ct);
        var platformIds = platform.Select(c => c.Id).ToList();
        var lessons = await _db.Set<CourseLesson>().Where(l => platformIds.Contains(l.CourseId)).ToListAsync(ct);
        var progress = await _db.Set<CourseLessonProgress>()
            .Where(p => platformIds.Contains(p.CourseId))
            .Select(p => new { p.Id, p.LessonId })
            .ToListAsync(ct);
        var progressByLesson = progress.GroupBy(p => p.LessonId).ToDictionary(g => g.Key, g => g.Count());
        var schoolCourses = await _db.Set<Course>().CountAsync(c => c.SchoolUserId != null, ct);

        await using var tx = apply && _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        var now = DateTime.UtcNow;
        var reports = new List<CurriculumCourseDto>();
        var removedLessonIds = new List<int>();

        foreach (var seed in curriculum)
        {
            var course = platform.FirstOrDefault(c => c.Slug == seed.Slug);
            if (course is null)
            {
                reports.Add(new CurriculumCourseDto(
                    seed.Slug,
                    seed.Title,
                    "create",
                    0,
                    seed.Lessons.Count,
                    false,
                    seed.Lessons.Select((l, i) => Lesson(l.Key, l.Title, "create", null, null, i, 0)).ToList()));
                if (apply)
                {
                    await CreateCourseAsync(seed, now, ct);
                }

                continue;
            }

            if (!course.IsActive)
            {
                reports.Add(new CurriculumCourseDto(seed.Slug, course.Title, "skip-inactive", 0, 0, false, []));
                continue;
            }

            var mine = lessons.Where(l => l.CourseId == course.Id).OrderBy(l => l.Position).ThenBy(l => l.Id).ToList();
            var untouched = mine.Count > 0 && mine.All(l => l.SeedKey is null && l.UpdatedAt == course.UpdatedAt);
            var keys = ResolveKeys(seed.Slug, mine);
            var seedByKey = seed.Lessons.ToDictionary(l => l.Key);

            bool Edited(CourseLesson l)
            {
                var current = CourseSeed.Fingerprint(l.Title, l.Summary, l.EstimatedMinutes, l.ContentJson);
                if (l.SeedHash is not null)
                {
                    return current != l.SeedHash;
                }

                if (untouched)
                {
                    return false;
                }

                return !(keys.TryGetValue(l.Id, out var k) && k is not null && seedByKey.TryGetValue(k, out var s) && s.Hash == current);
            }

            var items = new List<CurriculumLessonDto>();
            var order = new List<CourseLesson?>();
            var actions = new List<Action>();

            foreach (var s in seed.Lessons)
            {
                var existing = mine.FirstOrDefault(l => keys[l.Id] == s.Key);
                var position = order.Count;
                if (existing is null)
                {
                    items.Add(Lesson(s.Key, s.Title, "create", null, null, position, 0));
                    order.Add(null);
                    actions.Add(() => _db.Set<CourseLesson>().Add(CourseSeed.NewLesson(course.Id, position, s, now)));
                    continue;
                }

                var count = progressByLesson.GetValueOrDefault(existing.Id);
                order.Add(existing);
                if (Edited(existing))
                {
                    var (manual, alike, missing) = CompareQuestions(existing.ContentJson, s.Content);
                    items.Add(new CurriculumLessonDto(
                        s.Key,
                        existing.Title,
                        "keep-edited",
                        "Editada a mano: se conserva exactamente como está.",
                        existing.Position + 1,
                        position + 1,
                        count,
                        manual,
                        alike,
                        missing));
                    actions.Add(() => existing.AssignSeedKey(s.Key));
                    continue;
                }

                var same = CourseSeed.Fingerprint(existing.Title, existing.Summary, existing.EstimatedMinutes, existing.ContentJson) == s.Hash;
                items.Add(Lesson(s.Key, s.Title, same ? "unchanged" : "update", same ? null : $"Antes: «{existing.Title}»", existing.Position, position, count));
                actions.Add(() =>
                {
                    if (!same)
                    {
                        existing.Update(s.Title, s.Summary, s.Minutes, s.Content, now);
                    }

                    existing.MarkSeeded(s.Key, s.Hash);
                });
            }

            foreach (var l in mine.Where(l => !order.Contains(l)))
            {
                var key = keys[l.Id];
                var count = progressByLesson.GetValueOrDefault(l.Id);
                if (key is not null && CourseSeed.RetiredLessons.TryGetValue(key, out var targets))
                {
                    var into = string.Join(", ", targets.Select(t => $"«{titles.GetValueOrDefault(t, t)}»"));
                    if (!Edited(l))
                    {
                        items.Add(Lesson(key, l.Title, "remove", $"Se integra en {into}.", l.Position, null, count));
                        removedLessonIds.Add(l.Id);
                        actions.Add(() => _db.Set<CourseLesson>().Remove(l));
                        continue;
                    }

                    var (manual, alike, missing) = CompareQuestions(l.ContentJson, "[]");
                    items.Add(new CurriculumLessonDto(
                        key,
                        l.Title,
                        "keep-edited",
                        $"Editada a mano: se conserva aunque la malla la integra en {into}.",
                        l.Position + 1,
                        order.Count + 1,
                        count,
                        manual,
                        alike,
                        missing));
                }
                else
                {
                    items.Add(new CurriculumLessonDto(
                        key,
                        l.Title,
                        "keep-unknown",
                        "No pertenece a la malla de Luz Verde: se conserva.",
                        l.Position + 1,
                        order.Count + 1,
                        count,
                        [],
                        0,
                        0));
                }

                order.Add(l);
            }

            var edited = items.Any(i => i.Action is "keep-edited" or "keep-unknown");
            var changed = items.Any(i => i.Action is "create" or "update" or "remove" || i.PositionBefore != i.PositionAfter)
                || (!edited && course.Description != seed.Description);
            reports.Add(new CurriculumCourseDto(
                seed.Slug,
                course.Title,
                changed ? "update" : "unchanged",
                mine.Count,
                order.Count,
                edited,
                items));

            if (!apply)
            {
                continue;
            }

            foreach (var act in actions)
            {
                act();
            }

            if (!changed)
            {
                continue;
            }

            for (var i = 0; i < order.Count; i++)
            {
                if (order[i] is { } l && l.Position != i)
                {
                    l.MoveTo(i, now);
                }
            }

            course.Update(
                course.Title,
                edited ? course.Description : seed.Description,
                edited ? course.Category : seed.Category,
                course.CoverUrl,
                course.IsPublished,
                now);
        }

        var lessonsAfter = reports.Sum(r => r.LessonsAfter);
        var progressToRemove = resetProgress
            ? progress.Count
            : progress.Count(p => removedLessonIds.Contains(p.LessonId));

        if (apply)
        {
            var doomed = resetProgress
                ? await _db.Set<CourseLessonProgress>().Where(p => platformIds.Contains(p.CourseId)).ToListAsync(ct)
                : await _db.Set<CourseLessonProgress>().Where(p => removedLessonIds.Contains(p.LessonId)).ToListAsync(ct);
            _db.Set<CourseLessonProgress>().RemoveRange(doomed);
            await _db.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }
        }

        return new CurriculumPlanDto(
            apply,
            apply && resetProgress,
            lessons.Count,
            lessonsAfter,
            curriculum.Sum(c => c.Lessons.Count),
            progress.Count,
            progressToRemove,
            schoolCourses,
            reports);
    }

    private async Task CreateCourseAsync(CourseSeed.SeedCourse seed, DateTime now, CancellationToken ct)
    {
        var course = Course.Create(null, 0, seed.Title, seed.Description, seed.Category, seed.CoverUrl, now, seed.Slug);
        _db.Set<Course>().Add(course);
        await _db.SaveChangesAsync(ct);
        for (var i = 0; i < seed.Lessons.Count; i++)
        {
            _db.Set<CourseLesson>().Add(CourseSeed.NewLesson(course.Id, i, seed.Lessons[i], now));
        }

        course.Update(course.Title, course.Description, course.Category, course.CoverUrl, true, now);
    }

    /// <summary>Seed key of each lesson: the stored one, or the one its title had in a previous curriculum.</summary>
    private static Dictionary<int, string?> ResolveKeys(string slug, List<CourseLesson> lessons)
    {
        var taken = new HashSet<string>();
        var keys = new Dictionary<int, string?>();
        foreach (var l in lessons.Where(l => l.SeedKey is not null))
        {
            keys[l.Id] = taken.Add(l.SeedKey!) ? l.SeedKey : null;
        }

        foreach (var l in lessons.Where(l => l.SeedKey is null))
        {
            var key = CourseSeed.LegacyKey(slug, l.Title);
            keys[l.Id] = key is not null && taken.Add(key) ? key : null;
        }

        return keys;
    }

    /// <summary>Interactive blocks of an edited lesson that do not appear in the seed version, and how many do.</summary>
    private static (IReadOnlyList<string> Manual, int Alike, int SeedMissing) CompareQuestions(string currentJson, string seedJson)
    {
        var current = Interactive(currentJson);
        var seed = Interactive(seedJson);
        var seedRaw = seed.Select(b => b.Raw).ToHashSet();
        var currentRaw = current.Select(b => b.Raw).ToHashSet();
        var manual = current.Where(b => !seedRaw.Contains(b.Raw)).Select(b => b.Label).ToList();
        return (manual, current.Count - manual.Count, seed.Count(b => !currentRaw.Contains(b.Raw)));
    }

    private static List<(string Raw, string Label)> Interactive(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return doc.RootElement.EnumerateArray()
                .Where(b => b.ValueKind == JsonValueKind.Object
                    && b.TryGetProperty("type", out var t)
                    && InteractiveTypes.Contains(t.GetString() ?? ""))
                .Select(b => (b.GetRawText(), Label(b)))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Label(JsonElement block)
    {
        var type = block.GetProperty("type").GetString();
        foreach (var name in new[] { "question", "statement", "situation", "instructions", "text" })
        {
            if (block.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
            {
                var text = v.GetString()!.Trim();
                return $"[{type}] {(text.Length > 160 ? text[..160] + "…" : text)}";
            }
        }

        return $"[{type}]";
    }

    private static CurriculumLessonDto Lesson(string? key, string title, string action, string? detail, int? before, int? after, int progress) =>
        new(key, title, action, detail, before + 1, after + 1, progress, [], 0, 0);
}

public sealed record CurriculumPlanDto(
    bool Applied,
    bool ProgressReset,
    int LessonsBefore,
    int LessonsAfter,
    int SeedLessons,
    int ProgressRows,
    int ProgressRowsRemoved,
    int SchoolCoursesUntouched,
    IReadOnlyList<CurriculumCourseDto> Courses);

public sealed record CurriculumCourseDto(
    string Slug,
    string Title,
    string Action,
    int LessonsBefore,
    int LessonsAfter,
    bool HasEditedLessons,
    IReadOnlyList<CurriculumLessonDto> Lessons);

public sealed record CurriculumLessonDto(
    string? Key,
    string Title,
    string Action,
    string? Detail,
    int? PositionBefore,
    int? PositionAfter,
    int Progress,
    IReadOnlyList<string> ManualQuestions,
    int QuestionsLikeSeed,
    int SeedQuestionsMissing);
