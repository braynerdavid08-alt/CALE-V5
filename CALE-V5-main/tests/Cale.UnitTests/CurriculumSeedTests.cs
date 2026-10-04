using System.Text.Json;
using Cale.Api.Services.Courses;
using Cale.Modules.Catalog.Domain;

namespace Cale.UnitTests;

public sealed class CurriculumSeedTests
{
    [Fact]
    public void Curriculum_has_the_agreed_shape()
    {
        var curriculum = CourseSeed.Curriculum();
        var counts = curriculum.ToDictionary(c => c.Slug, c => c.Lessons.Count);

        Assert.Equal(57, counts.Values.Sum());
        Assert.Equal(5, counts[CourseSeed.SignsSlug]);
        Assert.Equal(4, counts[CourseSeed.SignageSlug]);
        Assert.Equal(6, counts[CourseSeed.MobilitySlug]);
        Assert.Equal(9, counts[CourseSeed.RulesSlug]);
        Assert.Equal(7, counts[CourseSeed.FirstAidSlug]);
        Assert.Equal(5, counts[CourseSeed.RoadSlug]);
        Assert.Equal(5, counts[CourseSeed.VehicleSlug]);
        Assert.Equal(6, counts[CourseSeed.MotorcycleSlug]);
        Assert.Equal(5, counts[CourseSeed.CarSlug]);
        Assert.Equal(5, counts[CourseSeed.PublicServiceSlug]);
    }

    [Fact]
    public void Lesson_keys_are_stable_unique_and_scoped_to_their_course()
    {
        var curriculum = CourseSeed.Curriculum();
        var keys = curriculum.SelectMany(c => c.Lessons.Select(l => l.Key)).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        foreach (var course in curriculum)
        {
            Assert.All(course.Lessons, l => Assert.StartsWith(course.Slug + "/", l.Key));
            Assert.Equal(course.Lessons.Count, course.Lessons.Select(l => l.Title).Distinct().Count());
            Assert.All(course.Lessons, l => Assert.Equal(l.Key, CourseSeed.LegacyKey(course.Slug, l.Title)));
        }
    }

    [Fact]
    public void Retired_lessons_point_to_lessons_that_exist()
    {
        var keys = CourseSeed.Curriculum().SelectMany(c => c.Lessons.Select(l => l.Key)).ToHashSet();

        Assert.All(CourseSeed.RetiredLessons, r =>
        {
            Assert.DoesNotContain(r.Key, keys);
            Assert.All(r.Value, target => Assert.Contains(target, keys));
        });
    }

    [Fact]
    public void Curriculum_tree_names_lessons_that_exist()
    {
        var titles = CourseSeed.Curriculum().ToDictionary(c => c.Slug, c => c.Lessons.Select(l => l.Title).ToHashSet());
        var broken = CurriculumTree.Nuclei
            .SelectMany(n => n.Themes)
            .SelectMany(t => t.Subtopics)
            .Where(s => s.LessonTitle is not null
                && !(titles.TryGetValue(s.CourseSlug!, out var set) && set.Contains(s.LessonTitle)))
            .Select(s => $"{s.CourseSlug}: {s.LessonTitle}")
            .Distinct()
            .ToList();

        Assert.True(broken.Count == 0, "Curriculum tree points to missing lessons:\n" + string.Join("\n", broken));
    }

    [Fact]
    public void No_seed_text_is_cut_by_the_content_limits()
    {
        var cut = new List<string>();
        foreach (var lesson in CourseSeed.Curriculum().SelectMany(c => c.Lessons))
        {
            Assert.NotEqual("[]", lesson.Content);
            Assert.InRange(lesson.Minutes, 1, 240);
            using var doc = JsonDocument.Parse(lesson.Content);
            Walk(doc.RootElement, "", lesson.Key, cut);
        }

        Assert.True(cut.Count == 0, "Texts truncated to a limit:\n" + string.Join("\n", cut));
    }

    private static int[] Limits(string property) => property switch
    {
        "body" or "explanation" => [6000],
        "back" or "note" => [400, 800],
        _ => [400]
    };

    private static void Walk(JsonElement e, string property, string key, List<string> cut)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.String when Limits(property).Contains(e.GetString()!.Length):
                cut.Add($"{key} {property}: {e.GetString()![..40]}…");
                break;
            case JsonValueKind.Array:
                foreach (var child in e.EnumerateArray())
                {
                    Walk(child, property, key, cut);
                }

                break;
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    Walk(p.Value, p.Name, key, cut);
                }

                break;
        }
    }
}
