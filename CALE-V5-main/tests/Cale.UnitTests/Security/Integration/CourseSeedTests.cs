using System.Text.Json;
using Cale.Api.Services.Courses;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Security.Integration;

/// <summary>
/// The course seed swallows its own errors at startup, so an invalid block would silently leave a
/// course out. These checks run against the throwaway database seeded on boot.
/// </summary>
public sealed class CourseSeedTests(SecurityApiFixture fixture) : IClassFixture<SecurityApiFixture>
{
    private readonly SecurityApiFactory _api = fixture.Factory;

    [Fact]
    public async Task Every_platform_course_is_seeded_with_lessons()
    {
        string[] slugs =
        [
            CourseSeed.SignsSlug, CourseSeed.RulesSlug, CourseSeed.SignageSlug, CourseSeed.FirstAidSlug,
            CourseSeed.MobilitySlug, CourseSeed.RoadSlug, CourseSeed.VehicleSlug, CourseSeed.MotorcycleSlug,
            CourseSeed.CarSlug, CourseSeed.PublicServiceSlug
        ];

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        foreach (var slug in slugs)
        {
            var course = await db.Set<Course>().AsNoTracking().SingleOrDefaultAsync(c => c.Slug == slug);
            Assert.True(course is not null, $"Course {slug} was not seeded.");
            Assert.True(await db.Set<CourseLesson>().AnyAsync(l => l.CourseId == course!.Id), $"Course {slug} has no lessons.");
        }
    }

    [Fact]
    public async Task Motorcycle_preride_lesson_covers_fluids_and_preventive_maintenance()
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var courseId = await db.Set<Course>().Where(c => c.Slug == CourseSeed.MotorcycleSlug).Select(c => c.Id).SingleAsync();
        var lesson = await db.Set<CourseLesson>().AsNoTracking()
            .SingleAsync(l => l.CourseId == courseId && l.Title == "Revisión preoperacional de la motocicleta");

        var titles = JsonDocument.Parse(lesson.ContentJson).RootElement.EnumerateArray()
            .Select(b => b.TryGetProperty("title", out var t) ? t.GetString() : null)
            .ToList();

        Assert.Contains("Revisión preoperacional y mantenimiento preventivo", titles);
        Assert.Contains("Cada fluido en su lugar", titles);
        Assert.Contains("Limpieza y herramientas en casa", titles);
        Assert.Contains("refrigerante", lesson.ContentJson);
    }
}
