using Cale.Api.Services.Courses;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Domain;
using Cale.Modules.Courses.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cale.UnitTests;

public sealed class CurriculumSyncTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly CaleDbContext _db;
    private readonly CurriculumSync _sync;
    private readonly User _school;
    private readonly User _student;

    public CurriculumSyncTests()
    {
        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, CoursesModelKey>()
            .Options;
        _db = new CaleDbContext(
            options,
            new MappingAssemblies(typeof(UserConfiguration).Assembly, typeof(CourseConfiguration).Assembly));
        _db.Database.EnsureCreated();
        _school = User.RegisterSchool("Escuela A", "a@test.co", "x", Now);
        _db.Add(_school);
        _db.SaveChanges();
        _student = User.RegisterStudent("Ana", "ana@test.co", "x", Now, _school.Id);
        _db.Add(_student);
        _db.SaveChanges();
        _sync = new CurriculumSync(_db, NullLogger<CurriculumSync>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private sealed class CoursesModelKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => (context.GetType(), nameof(CoursesModelKey), designTime);
    }

    [Fact]
    public async Task Fresh_seed_has_nothing_to_change()
    {
        await new CourseSeed(_db).EnsureAsync(null);

        var plan = await _sync.PlanAsync(default);

        Assert.Equal(57, plan.LessonsBefore);
        Assert.Equal(57, plan.LessonsAfter);
        Assert.Equal(57, plan.SeedLessons);
        Assert.All(plan.Courses, c => Assert.Equal("unchanged", c.Action));
    }

    [Fact]
    public async Task Legacy_courses_are_merged_by_title_and_edits_survive()
    {
        var legacy = await BuildLegacyAsync();
        var editedJson = legacy.EditedLesson.ContentJson;

        var plan = await _sync.PlanAsync(default);

        Assert.Equal(57, plan.LessonsAfter);
        Assert.Equal(3, plan.ProgressRows);
        Assert.Equal(1, plan.SchoolCoursesUntouched);
        var mobility = Assert.Single(plan.Courses, c => c.Slug == CourseSeed.MobilitySlug);
        Assert.Contains(mobility.Lessons, l => l.Key == $"{CourseSeed.MobilitySlug}/vision-cero" && l.Action == "remove");
        Assert.Contains(mobility.Lessons, l => l.Key == $"{CourseSeed.MobilitySlug}/visibilidad-clima" && l.Action == "create");
        var aid = Assert.Single(plan.Courses, c => c.Slug == CourseSeed.FirstAidSlug);
        Assert.Contains(aid.Lessons, l => l.Key == $"{CourseSeed.FirstAidSlug}/derrames" && l.Action == "create");
        var kept = Assert.Single(aid.Lessons, l => l.Action == "keep-edited");
        Assert.Contains(kept.ManualQuestions, q => q.Contains("PREGUNTA MANUAL"));
        Assert.Equal(60, plan.LessonsBefore);

        var applied = await _sync.ApplyAsync(resetProgress: true, default);

        Assert.Equal(3, applied.ProgressRowsRemoved);
        Assert.False(await _db.Set<CourseLessonProgress>().AnyAsync());
        Assert.Equal(editedJson, (await _db.Set<CourseLesson>().SingleAsync(l => l.Id == legacy.EditedLesson.Id)).ContentJson);
        var platformIds = await _db.Set<Course>().Where(c => c.SchoolUserId == null).Select(c => c.Id).ToListAsync();
        Assert.Equal(57, await _db.Set<CourseLesson>().CountAsync(l => platformIds.Contains(l.CourseId)));
        var school = await _db.Set<CourseLesson>().SingleAsync(l => l.CourseId == legacy.SchoolCourseId);
        Assert.Equal("Visión Cero: ninguna muerte en la vía es aceptable", school.Title);

        var again = await _sync.PlanAsync(default);
        Assert.Empty(again.Courses.SelectMany(c => c.Lessons.Where(l => l.Action != "unchanged" && l.Action != "keep-edited").Select(l => $"{c.Slug} {l.Key} {l.Action} {l.PositionBefore}->{l.PositionAfter}")));
        Assert.All(again.Courses, c => Assert.Equal("unchanged", c.Action));
        Assert.Single(again.Courses.SelectMany(c => c.Lessons), l => l.Action == "keep-edited");
    }

    [Fact]
    public async Task Without_reset_only_progress_of_removed_lessons_goes_away()
    {
        var legacy = await BuildLegacyAsync();

        var applied = await _sync.ApplyAsync(resetProgress: false, default);

        Assert.Equal(1, applied.ProgressRowsRemoved);
        var lessonIds = await _db.Set<CourseLesson>().Select(l => l.Id).ToListAsync();
        var left = await _db.Set<CourseLessonProgress>().ToListAsync();
        Assert.Equal(2, left.Count);
        Assert.All(left, p => Assert.Contains(p.LessonId, lessonIds));
        Assert.DoesNotContain(left, p => p.LessonId == legacy.RetiredLessonId);
    }

    [Fact]
    public async Task Lesson_links_find_published_platform_lessons_even_when_retitled()
    {
        await new CourseSeed(_db).EnsureAsync(null);
        var signs = await _db.Set<Course>().SingleAsync(c => c.Slug == CourseSeed.SignsSlug);
        var lesson = await _db.Set<CourseLesson>().SingleAsync(l => l.SeedKey == $"{CourseSeed.SignsSlug}/preventivas");
        _db.Entry(lesson).Property(l => l.Title).CurrentValue = "Preventivas (versión de la escuela)";
        _db.Add(CourseLessonProgress.Create(signs.Id, lesson.Id, _student.Id, 100, Now));
        var rules = await _db.Set<Course>().SingleAsync(c => c.Slug == CourseSeed.RulesSlug);
        rules.Update(rules.Title, rules.Description, rules.Category, rules.CoverUrl, false, Now);
        await _db.SaveChangesAsync();

        var links = await LessonLinks.ResolveAsync(
            _db,
            [
                (CourseSeed.SignsSlug, "Señales preventivas"),
                (CourseSeed.RulesSlug, "Prelación: quién pasa primero"),
                (CourseSeed.SignsSlug, "Una lección que no existe")
            ],
            _student.Id,
            default);

        var link = Assert.Single(links).Value;
        Assert.Equal(lesson.Id, link.LessonId);
        Assert.Equal("Preventivas (versión de la escuela)", link.LessonTitle);
        Assert.True(link.Completed);
    }

    private sealed record Legacy(CourseLesson EditedLesson, int RetiredLessonId, int SchoolCourseId);

    /// <summary>The platform as an older seed left it: no keys, former titles, retired lessons and one hand edit.</summary>
    private async Task<Legacy> BuildLegacyAsync()
    {
        await new CourseSeed(_db).EnsureAsync(null);
        var courses = await _db.Set<Course>().ToListAsync();
        var lessons = await _db.Set<CourseLesson>().ToListAsync();
        foreach (var l in lessons)
        {
            _db.Entry(l).Property(x => x.SeedKey).CurrentValue = null;
            _db.Entry(l).Property(x => x.SeedHash).CurrentValue = null;
        }

        int Id(string slug) => courses.Single(c => c.Slug == slug).Id;
        CourseLesson Find(string slug, string title) => lessons.Single(l => l.CourseId == Id(slug) && l.Title == title);
        void Add(string slug, string title) =>
            _db.Add(Old(Id(slug), 90 + lessons.Count, title));

        Rename(Find(CourseSeed.MobilitySlug, "Movilidad sostenible y conducción eficiente"), "Movilidad sostenible y conducción responsable");
        Rename(Find(CourseSeed.RulesSlug, "Documentos, habilitación y restricciones para circular"), "Documentos y habilitación para circular");
        Rename(Find(CourseSeed.MotorcycleSlug, "Lluvia, calor y fatiga"), "Lluvia, calor, fatiga y puntos ciegos");
        _db.Remove(Find(CourseSeed.MobilitySlug, "Visibilidad y clima: noche, lluvia y luces"));
        _db.Remove(Find(CourseSeed.MotorcycleSlug, "Posición en la vía, puntos ciegos y tráfico urbano"));
        _db.Remove(Find(CourseSeed.FirstAidSlug, "Actuación ante derrames o peligros en la vía"));
        Add(CourseSeed.MobilitySlug, "Visión Cero: ninguna muerte en la vía es aceptable");
        Add(CourseSeed.MobilitySlug, "Tolerancia del cuerpo humano al impacto");
        Add(CourseSeed.MobilitySlug, "Conducción eficiente y eco-conducción");
        Add(CourseSeed.RulesSlug, "Restricciones urbanas y conducta de los demás");
        Add(CourseSeed.SignageSlug, "El sistema de señalización");
        Add(CourseSeed.SignageSlug, "Las familias de señales verticales");
        await _db.SaveChangesAsync();

        foreach (var c in courses)
        {
            foreach (var l in await _db.Set<CourseLesson>().Where(l => l.CourseId == c.Id).ToListAsync())
            {
                _db.Entry(l).Property(x => x.UpdatedAt).CurrentValue = c.UpdatedAt;
            }
        }

        var edited = Find(CourseSeed.FirstAidSlug, "Heridas y hemorragias");
        var field = new[] { "\"question\":\"", "\"statement\":\"", "\"situation\":\"" }.First(edited.ContentJson.Contains);
        var editedAt = Now.AddDays(1);
        edited.Update(edited.Title, edited.Summary, edited.EstimatedMinutes, ReplaceFirst(edited.ContentJson, field, field + "PREGUNTA MANUAL "), editedAt);
        courses.Single(c => c.Slug == CourseSeed.FirstAidSlug).Touch(editedAt);

        var school = Course.Create(_school.Id, _school.Id, "Copia de la escuela", null, "Seguridad vial", null, Now);
        _db.Add(school);
        await _db.SaveChangesAsync();
        _db.Add(Old(school.Id, 0, "Visión Cero: ninguna muerte en la vía es aceptable"));

        var mobilityId = Id(CourseSeed.MobilitySlug);
        var retired = await _db.Set<CourseLesson>().SingleAsync(l => l.Title == "Visión Cero: ninguna muerte en la vía es aceptable" && l.CourseId == mobilityId);
        var kept = Find(CourseSeed.RulesSlug, "Prelación: quién pasa primero");
        _db.AddRange(
            CourseLessonProgress.Create(retired.CourseId, retired.Id, _student.Id, 100, Now),
            CourseLessonProgress.Create(kept.CourseId, kept.Id, _student.Id, 100, Now),
            CourseLessonProgress.Create(edited.CourseId, edited.Id, _student.Id, 100, Now));
        await _db.SaveChangesAsync();
        return new Legacy(edited, retired.Id, school.Id);
    }

    private void Rename(CourseLesson lesson, string title) =>
        _db.Entry(lesson).Property(x => x.Title).CurrentValue = title;

    private static CourseLesson Old(int courseId, int position, string title)
    {
        var lesson = CourseLesson.Create(courseId, position, title, Now);
        lesson.Update(title, "Versión anterior.", 10, "[{\"type\":\"text\",\"title\":\"Antes\",\"body\":\"Contenido anterior.\"}]", Now);
        return lesson;
    }

    private static string ReplaceFirst(string text, string find, string replace)
    {
        var at = text.IndexOf(find, StringComparison.Ordinal);
        return text[..at] + replace + text[(at + find.Length)..];
    }
}
