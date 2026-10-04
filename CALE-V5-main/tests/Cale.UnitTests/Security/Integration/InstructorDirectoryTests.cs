using System.Net;
using System.Net.Http.Json;
using Cale.Api.Services;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Security.Integration;

/// <summary>The public instructor directory only shows instructors who opted in.</summary>
public sealed class InstructorDirectoryTests(SecurityApiFixture fixture) : IClassFixture<SecurityApiFixture>
{
    private readonly SecurityApiFactory _api = fixture.Factory;

    [Fact]
    public async Task Directory_shows_only_instructors_who_opted_in_and_they_can_leave()
    {
        var u = _api.Users;
        using var anon = _api.ClientFor(null);
        using var asTeacher2 = _api.ClientFor(u.Teacher2);

        Assert.DoesNotContain(u.Teacher2.Name, await anon.GetStringAsync("/api/public/instructors"));
        Assert.False((await asTeacher2.GetFromJsonAsync<Listing>("/api/me/directory-listing"))!.Listed);

        var join = await asTeacher2.PutAsJsonAsync("/api/me/directory-listing", new { listed = true });
        join.EnsureSuccessStatusCode();
        var listed = await anon.GetStringAsync("/api/public/instructors");
        Assert.Contains(u.Teacher2.Name, listed);
        Assert.DoesNotContain(u.Teacher1.Name, listed);
        Assert.DoesNotContain("@pruebas.test", listed);
        Assert.True((await asTeacher2.GetFromJsonAsync<Listing>("/api/me/directory-listing"))!.Listed);

        (await asTeacher2.PutAsJsonAsync("/api/me/directory-listing", new { listed = false })).EnsureSuccessStatusCode();
        Assert.DoesNotContain(u.Teacher2.Name, await anon.GetStringAsync("/api/public/instructors"));
    }

    [Fact]
    public async Task Only_instructors_can_change_their_directory_listing()
    {
        var u = _api.Users;
        using var anon = _api.ClientFor(null);
        using var asStudent = _api.ClientFor(u.StudentA);
        using var asSchool = _api.ClientFor(u.School1);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PutAsJsonAsync("/api/me/directory-listing", new { listed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asStudent.PutAsJsonAsync("/api/me/directory-listing", new { listed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asSchool.PutAsJsonAsync("/api/me/directory-listing", new { listed = true })).StatusCode);

        var directory = await anon.GetStringAsync("/api/public/instructors");
        Assert.DoesNotContain(u.StudentA.Name, directory);
        Assert.DoesNotContain(u.School1.Name, directory);
    }

    [Fact]
    public async Task Directory_invitation_is_sent_once_even_if_the_instructor_deletes_it()
    {
        var teacher1 = _api.Users.Teacher1;
        using (var scope = _api.Services.CreateScope())
        {
            var home = scope.ServiceProvider.GetRequiredService<HomepageService>();
            var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
            Assert.True(await home.InviteInstructorsToDirectoryAsync(publisher, CancellationToken.None) > 0);
        }

        using var asTeacher1 = _api.ClientFor(teacher1);
        int notificationId;
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            notificationId = await db.Set<AppNotification>()
                .Where(n => n.UserId == teacher1.Id && n.DedupeKey == HomepageService.DirectoryInviteDedupeKey)
                .Select(n => n.Id)
                .SingleAsync();
        }

        (await asTeacher1.DeleteAsync($"/api/notifications/{notificationId}")).EnsureSuccessStatusCode();

        using (var scope = _api.Services.CreateScope())
        {
            var home = scope.ServiceProvider.GetRequiredService<HomepageService>();
            var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
            await home.InviteInstructorsToDirectoryAsync(publisher, CancellationToken.None);

            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            Assert.Equal(1, await db.Set<AppNotification>()
                .CountAsync(n => n.UserId == teacher1.Id && n.DedupeKey == HomepageService.DirectoryInviteDedupeKey));
        }
    }

    private sealed record Listing(bool Listed);
}
