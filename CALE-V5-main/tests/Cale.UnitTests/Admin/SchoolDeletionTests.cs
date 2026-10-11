using System.Net;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.UnitTests.Security.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cale.UnitTests.Admin;

public sealed class SchoolDeletionTests : IAsyncLifetime
{
    private readonly SecurityApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        _ = _factory.Server;
        await _factory.SeedAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Empty_school_is_deleted_with_its_profile()
    {
        var schoolId = await AddEmptySchoolAsync();
        using var admin = _factory.ClientFor(_factory.Users.Admin);

        var response = await admin.DeleteAsync($"/api/admin/schools/{schoolId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        Assert.False(await db.Set<User>().AnyAsync(u => u.Id == schoolId));
        Assert.False(await db.Set<SchoolProfile>().AnyAsync(p => p.UserId == schoolId));
    }

    [Theory]
    [InlineData("/api/admin/schools/{0}")]
    [InlineData("/api/admin/users/{0}")]
    public async Task School_with_teachers_or_students_cannot_be_deleted(string route)
    {
        var schoolId = _factory.Users.School2.Id;
        using var admin = _factory.ClientFor(_factory.Users.Admin);

        var response = await admin.DeleteAsync(string.Format(route, schoolId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("school_not_empty", await response.Content.ReadAsStringAsync());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        Assert.True(await db.Set<User>().AnyAsync(u => u.Id == schoolId));
        Assert.True(await db.Set<User>().AnyAsync(u => u.Id == _factory.Users.StudentC.Id && u.SchoolId == schoolId));
    }

    [Fact]
    public async Task Only_admins_can_delete_schools()
    {
        var schoolId = await AddEmptySchoolAsync();
        using var school = _factory.ClientFor(_factory.Users.School1);

        var response = await school.DeleteAsync($"/api/admin/schools/{schoolId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_non_school_through_the_school_route_is_rejected()
    {
        using var admin = _factory.ClientFor(_factory.Users.Admin);

        var response = await admin.DeleteAsync($"/api/admin/schools/{_factory.Users.StudentA.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> AddEmptySchoolAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var now = DateTime.UtcNow;
        var school = User.RegisterSchool(
            "Escuela Duplicada",
            $"duplicada-{Guid.NewGuid():N}@pruebas.test",
            hasher.Hash(SecurityApiFactory.Password),
            now);
        db.Add(school);
        await db.SaveChangesAsync();
        db.Add(SchoolProfile.CreateDraft(school.Id, school.Name, school.Email, SchoolPlans.Find(SchoolPlans.Monthly)!, now));
        await db.SaveChangesAsync();
        return school.Id;
    }
}
