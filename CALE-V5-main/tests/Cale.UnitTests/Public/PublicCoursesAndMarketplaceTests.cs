using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.UnitTests.Security.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cale.UnitTests.Public;

public sealed class PublicCoursesAndMarketplaceTests : IAsyncLifetime
{
    private readonly SecurityApiFactory _api = new();

    public async Task InitializeAsync()
    {
        _ = _api.Server;
        await _api.SeedAsync();
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var listing = await db.Set<InstructorListing>()
            .FirstOrDefaultAsync(l => l.UserId == _api.Users.Teacher1.Id);
        if (listing is null)
        {
            listing = InstructorListing.Create(_api.Users.Teacher1.Id, DateTime.UtcNow);
            db.Add(listing);
            await db.SaveChangesAsync();
        }

        listing.SetAcceptsPrivateClasses(true);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Public_courses_list_returns_curriculum_covers()
    {
        var anon = _api.ClientFor(null);
        var list = await anon.GetFromJsonAsync<JsonElement>("/api/public/courses");
        Assert.True(list.GetArrayLength() >= 8);
        Assert.Contains(list.EnumerateArray(), c => c.GetProperty("slug").GetString() == "senales-transito");
        Assert.False(string.IsNullOrWhiteSpace(list[0].GetProperty("coverUrl").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(list[0].GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Student_can_request_private_class_and_rate_listed_instructor()
    {
        using var student = _api.ClientFor(_api.Users.StudentA);

        var request = await student.PostAsJsonAsync("/api/private-classes", new
        {
            instructorId = _api.Users.Teacher1.Id,
            category = "B1",
            preferredSchedule = "Sábados por la mañana",
            message = "Quiero practicar estacionamiento en paralelo."
        });
        Assert.True(request.IsSuccessStatusCode, await request.Content.ReadAsStringAsync());

        var rate = await student.PutAsJsonAsync($"/api/instructors/{_api.Users.Teacher1.Id}/rating", new
        {
            stars = 5,
            comment = "Muy claro explicando."
        });
        Assert.True(rate.IsSuccessStatusCode, await rate.Content.ReadAsStringAsync());

        var cards = await _api.ClientFor(null).GetFromJsonAsync<JsonElement>("/api/public/instructors?take=20");
        var teacher = cards.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == _api.Users.Teacher1.Id);
        Assert.True(teacher.GetProperty("acceptsPrivateClasses").GetBoolean());
        Assert.Equal(5, teacher.GetProperty("averageStars").GetDouble());
        Assert.Equal(1, teacher.GetProperty("reviewCount").GetInt32());
    }
}
