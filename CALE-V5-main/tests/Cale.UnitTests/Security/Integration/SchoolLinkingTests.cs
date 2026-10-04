using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Security.Integration;

/// <summary>
/// Schools never create accounts and never link someone without consent; nobody changes their login email.
/// </summary>
[Collection(SecurityApiCollection.Name)]
public sealed class SchoolLinkingTests(SecurityApiFixture fixture)
{
    private readonly SecurityApiFactory _api = fixture.Factory;
    private SecurityApiFactory.Accounts U => _api.Users;

    [Theory]
    [InlineData("/api/school/members")]
    [InlineData("/api/school/members/attach")]
    [InlineData("/api/school/imports/preview")]
    public async Task School_account_creation_and_silent_attach_endpoints_are_gone(string path)
    {
        var res = await _api.ClientFor(U.School1).PostAsJsonAsync(
            path,
            new { name = "Nueva", email = "nueva.cuenta@pruebas.test", password = "Prueba-Segura-2026!", role = "Student" });

        Assert.True(
            res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"got {(int)res.StatusCode}");
        await using var scope = _api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        Assert.False(await db.Set<User>().AnyAsync(x => x.Email == "nueva.cuenta@pruebas.test"));
    }

    [Fact]
    public async Task Invitation_links_only_after_the_member_accepts()
    {
        var school = _api.ClientFor(U.School1);
        var invite = await school.PostAsJsonAsync(
            "/api/school/invitations",
            new { email = U.LoneInvitedStudent.Email, role = "Student" });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        Assert.Null((await Reload(U.LoneInvitedStudent.Id)).SchoolId);

        var inviteId = await FindPendingIdAsync(U.LoneInvitedStudent, "Invite");

        var otherStudent = await _api.ClientFor(U.StudentC)
            .PostAsync($"/api/me/school-membership/invitations/{inviteId}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, otherStudent.StatusCode);

        var schoolAcceptsOwnInvite = await school.PostAsync($"/api/school/join-requests/{inviteId}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, schoolAcceptsOwnInvite.StatusCode);

        var otherSchoolCancels = await _api.ClientFor(U.School2)
            .PostAsync($"/api/school/invitations/{inviteId}/cancel", null);
        Assert.Equal(HttpStatusCode.Forbidden, otherSchoolCancels.StatusCode);
        Assert.Null((await Reload(U.LoneInvitedStudent.Id)).SchoolId);

        var accept = await _api.ClientFor(U.LoneInvitedStudent)
            .PostAsync($"/api/me/school-membership/invitations/{inviteId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal(U.School1.Id, (await Reload(U.LoneInvitedStudent.Id)).SchoolId);
    }

    [Fact]
    public async Task Rejected_invitation_leaves_the_account_unlinked()
    {
        var invite = await _api.ClientFor(U.School1).PostAsJsonAsync(
            "/api/school/invitations",
            new { email = U.LoneRejectingStudent.Email, role = "Student" });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);

        var inviteId = await FindPendingIdAsync(U.LoneRejectingStudent, "Invite");
        var reject = await _api.ClientFor(U.LoneRejectingStudent)
            .PostAsync($"/api/me/school-membership/invitations/{inviteId}/reject", null);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        Assert.Null((await Reload(U.LoneRejectingStudent.Id)).SchoolId);

        var lateAccept = await _api.ClientFor(U.LoneRejectingStudent)
            .PostAsync($"/api/me/school-membership/invitations/{inviteId}/accept", null);
        Assert.Equal(HttpStatusCode.BadRequest, lateAccept.StatusCode);
        Assert.Null((await Reload(U.LoneRejectingStudent.Id)).SchoolId);
    }

    [Fact]
    public async Task Student_request_links_only_after_the_school_accepts()
    {
        var request = await _api.ClientFor(U.LoneRequestingStudent).PostAsJsonAsync(
            "/api/me/school-membership/requests",
            new { schoolQuery = U.School1.Email });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        Assert.Null((await Reload(U.LoneRequestingStudent.Id)).SchoolId);

        var requestId = await FindPendingIdAsync(U.LoneRequestingStudent, "Request");

        var memberAcceptsOwnRequest = await _api.ClientFor(U.LoneRequestingStudent)
            .PostAsync($"/api/me/school-membership/invitations/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, memberAcceptsOwnRequest.StatusCode);

        var otherSchool = await _api.ClientFor(U.School2).PostAsync($"/api/school/join-requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, otherSchool.StatusCode);
        Assert.Null((await Reload(U.LoneRequestingStudent.Id)).SchoolId);

        var accept = await _api.ClientFor(U.School1).PostAsync($"/api/school/join-requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal(U.School1.Id, (await Reload(U.LoneRequestingStudent.Id)).SchoolId);
    }

    [Fact]
    public async Task School_cannot_invite_someone_who_already_belongs_to_another_school()
    {
        var res = await _api.ClientFor(U.School1).PostAsJsonAsync(
            "/api/school/invitations",
            new { email = U.StudentC.Email, role = "Student" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(U.School2.Id, (await Reload(U.StudentC.Id)).SchoolId);
    }

    [Fact]
    public async Task User_cannot_change_own_login_email()
    {
        var client = _api.ClientFor(U.LoneTeacher);
        var change = await client.PutAsJsonAsync(
            "/api/auth/me",
            new { name = "Lone_Teacher", email = "secuestrada@pruebas.test" });
        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        Assert.Equal(U.LoneTeacher.Email, (await Reload(U.LoneTeacher.Id)).Email);

        var rename = await client.PutAsJsonAsync("/api/auth/me", new { name = "Lone Teacher Renombrado" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        var stored = await Reload(U.LoneTeacher.Id);
        Assert.Equal("Lone Teacher Renombrado", stored.Name);
        Assert.Equal(U.LoneTeacher.Email, stored.Email);
    }

    [Fact]
    public async Task School_cannot_change_a_member_login_email()
    {
        var res = await _api.ClientFor(U.School1).PutAsJsonAsync(
            $"/api/school/members/{U.StudentB.Id}",
            new { name = U.StudentB.Name, email = "otro.correo@pruebas.test" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(U.StudentB.Email, (await Reload(U.StudentB.Id)).Email);
    }

    private async Task<int> FindPendingIdAsync(User member, string direction)
    {
        var json = await _api.ClientFor(member).GetStringAsync("/api/me/school-membership");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray()
            .First(x => x.GetProperty("direction").GetString() == direction
                && x.GetProperty("status").GetString() == "Pending")
            .GetProperty("id").GetInt32();
    }

    private async Task<User> Reload(int id)
    {
        await using var scope = _api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        return await db.Set<User>().AsNoTracking().FirstAsync(x => x.Id == id);
    }
}
