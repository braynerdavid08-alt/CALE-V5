using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Domain;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cale.UnitTests.Security.Integration;

public sealed class PublicCourseCatalogTests : IAsyncLifetime
{
    private readonly SecurityApiFactory _api = new();

    public async Task InitializeAsync()
    {
        _ = _api.Server;
        await _api.SeedAsync();
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Anonymous_visitors_see_only_published_platform_courses()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var now = DateTime.UtcNow;
            var platform = Course.Create(null, 0, "Curso público de prueba", "Descripción", null, "/signals/SR-01.svg", now);
            platform.Update(platform.Title, platform.Description, platform.Category, platform.CoverUrl, true, now);
            var draft = Course.Create(null, 0, "Borrador de plataforma", null, null, null, now);
            var school = Course.Create(_api.Users.School1.Id, _api.Users.Teacher1.Id, "Curso privado de escuela", null, null, null, now);
            school.Update(school.Title, null, null, null, true, now);
            db.AddRange(platform, draft, school);
            await db.SaveChangesAsync();
        }

        var courses = await _api.ClientFor(null).GetFromJsonAsync<JsonElement>("/api/public/courses");
        var titles = courses.EnumerateArray().Select(c => c.GetProperty("title").GetString()).ToList();

        Assert.Contains("Curso público de prueba", titles);
        Assert.DoesNotContain("Borrador de plataforma", titles);
        Assert.DoesNotContain("Curso privado de escuela", titles);
        var card = courses.EnumerateArray().Single(c => c.GetProperty("title").GetString() == "Curso público de prueba");
        Assert.Equal("/signals/SR-01.svg", card.GetProperty("coverUrl").GetString());
    }
}
