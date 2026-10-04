using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Courses;

/// <summary>
/// Turns the lesson a curriculum subtopic names (course slug + lesson title) into a published platform lesson
/// the student can open. Matches by seed key first, so a lesson an admin retitled is still found.
/// </summary>
public static class LessonLinks
{
    public static async Task<Dictionary<(string Slug, string Title), LessonLinkDto>> ResolveAsync(
        CaleDbContext db,
        IEnumerable<(string Slug, string Title)> wanted,
        int studentUserId,
        CancellationToken ct)
    {
        var targets = wanted.Distinct().ToList();
        var result = new Dictionary<(string Slug, string Title), LessonLinkDto>();
        if (targets.Count == 0)
        {
            return result;
        }

        var slugs = targets.Select(t => t.Slug).Distinct().ToList();
        var courses = await db.Set<Course>().AsNoTracking()
            .Where(c => c.SchoolUserId == null && c.Slug != null && slugs.Contains(c.Slug) && c.IsActive && c.IsPublished)
            .Select(c => new { c.Id, Slug = c.Slug!, c.Title })
            .ToListAsync(ct);
        if (courses.Count == 0)
        {
            return result;
        }

        var courseIds = courses.Select(c => c.Id).ToList();
        var lessons = await db.Set<CourseLesson>().AsNoTracking()
            .Where(l => courseIds.Contains(l.CourseId))
            .Select(l => new { l.Id, l.CourseId, l.Title, l.SeedKey })
            .ToListAsync(ct);
        var lessonIds = lessons.Select(l => l.Id).ToList();
        var done = (await db.Set<CourseLessonProgress>().AsNoTracking()
                .Where(p => p.StudentUserId == studentUserId && lessonIds.Contains(p.LessonId))
                .Select(p => p.LessonId)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var target in targets)
        {
            var course = courses.FirstOrDefault(c => c.Slug == target.Slug);
            if (course is null)
            {
                continue;
            }

            var key = CourseSeed.LegacyKey(target.Slug, target.Title);
            var mine = lessons.Where(l => l.CourseId == course.Id).ToList();
            var lesson = (key is null ? null : mine.FirstOrDefault(l => l.SeedKey == key))
                ?? mine.FirstOrDefault(l => string.Equals(l.Title, target.Title, StringComparison.OrdinalIgnoreCase));
            if (lesson is not null)
            {
                result[target] = new LessonLinkDto(lesson.Id, course.Id, course.Title, lesson.Title, done.Contains(lesson.Id));
            }
        }

        return result;
    }
}

public sealed record LessonLinkDto(int LessonId, int CourseId, string CourseTitle, string LessonTitle, bool Completed);
