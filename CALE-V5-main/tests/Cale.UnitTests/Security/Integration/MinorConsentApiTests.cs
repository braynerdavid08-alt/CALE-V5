using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cale.UnitTests.Security.Integration;

public sealed class MinorConsentApiTests : IAsyncLifetime
{
    private const string MinorEmail = "luzverde.prueba.menor@gmail.com";
    private const string Password = "Prueba-Segura-2026!";
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

    private static readonly object Consent = new { acceptTerms = true, acknowledgePrivacy = true, authorizeDataProcessing = true };

    private static string MinorBirthDate => DateTime.UtcNow.AddYears(-15).ToString("yyyy-MM-dd");

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync());
    }

    private HttpClient Bearer(string token)
    {
        var client = _api.ClientFor(null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>En pruebas no hay correo real: se emite un enlace nuevo como lo haría el reenvío.</summary>
    private async Task<string> IssueGuardianLinkAsync(string email)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var userId = await db.Set<User>().Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        var declaration = await db.Set<AgeDeclaration>().SingleAsync(d => d.UserId == userId);
        var token = declaration.IssueLink(DateTime.UtcNow);
        await db.SaveChangesAsync();
        return token;
    }

    [Fact]
    public async Task Student_registration_requires_birth_date_and_guardian_when_minor()
    {
        var anon = _api.ClientFor(null);
        await AssertError(await anon.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Menor Prueba", email = MinorEmail, password = Password, consent = Consent
        }), HttpStatusCode.BadRequest, "birth_date_required");

        await AssertError(await anon.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Menor Prueba", email = MinorEmail, password = Password, consent = Consent,
            birthDate = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd")
        }), HttpStatusCode.BadRequest, "birth_date_invalid");

        await AssertError(await anon.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Menor Prueba", email = MinorEmail, password = Password, consent = Consent, birthDate = MinorBirthDate
        }), HttpStatusCode.BadRequest, "guardian_required");

        await AssertError(await anon.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Menor Prueba", email = MinorEmail, password = Password, consent = Consent, birthDate = MinorBirthDate,
            guardian = new { name = "Madre Prueba", relationship = "Madre", email = MinorEmail }
        }), HttpStatusCode.BadRequest, "guardian_email_same");

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        Assert.False(await db.Set<User>().AnyAsync(u => u.Email == MinorEmail));
    }

    [Fact]
    public async Task Guardian_authorizes_and_later_revokes_with_full_trace()
    {
        var anon = _api.ClientFor(null);
        var created = await Json(await anon.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Menor Prueba", email = MinorEmail, password = Password, consent = Consent, birthDate = MinorBirthDate,
            guardian = new { name = "Madre Prueba", relationship = "madre", email = "luzverde.prueba.madre@gmail.com" }
        }));
        var minor = Bearer(created.GetProperty("token").GetString()!);

        var pending = await Json(await minor.GetAsync("/api/legal/me/age"));
        Assert.True(pending.GetProperty("isMinor").GetBoolean());
        Assert.Equal("PENDING", pending.GetProperty("guardianStatus").GetString());
        Assert.Equal("Madre", pending.GetProperty("guardianRelationship").GetString());
        Assert.DoesNotContain("luzverde.prueba.madre@gmail.com", pending.GetRawText());

        await AssertError(await anon.GetAsync("/api/legal/guardian/no-existe"), HttpStatusCode.NotFound, "guardian_link_invalid");

        var token = await IssueGuardianLinkAsync(MinorEmail);
        var view = await Json(await anon.GetAsync($"/api/legal/guardian/{token}"));
        Assert.Equal("Menor", view.GetProperty("minorFirstName").GetString());
        Assert.DoesNotContain(MinorEmail, view.GetRawText());

        await AssertError(await anon.PostAsJsonAsync($"/api/legal/guardian/{token}/decision",
            new { decision = "authorize", confirmIsGuardian = false }), HttpStatusCode.BadRequest, "guardian_confirmation_required");

        var confirmed = await Json(await anon.PostAsJsonAsync($"/api/legal/guardian/{token}/decision",
            new { decision = "authorize", confirmIsGuardian = true }));
        Assert.Equal("CONFIRMED", confirmed.GetProperty("status").GetString());

        await AssertError(await anon.PostAsJsonAsync($"/api/legal/guardian/{token}/decision",
            new { decision = "reject", confirmIsGuardian = false }), HttpStatusCode.Conflict, "guardian_already_decided");

        var revoked = await Json(await anon.PostAsJsonAsync($"/api/legal/guardian/{token}/decision",
            new { decision = "revoke", confirmIsGuardian = false }));
        Assert.Equal("REVOKED", revoked.GetProperty("status").GetString());

        await AssertError(await anon.PostAsJsonAsync("/api/auth/login", new { email = MinorEmail, password = Password }),
            HttpStatusCode.Forbidden, "account_suspended");

        var admin = _api.ClientFor(_api.Users.Admin);
        var minors = await Json(await admin.GetAsync("/api/admin/legal/minors"));
        var row = minors.EnumerateArray().Single(m => m.GetProperty("userEmail").GetString() == MinorEmail);
        Assert.Equal("REVOKED", row.GetProperty("guardianStatus").GetString());
        var actions = row.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Contains("Declared", actions);
        Assert.Contains("EmailNotSent", actions);
        Assert.Contains("Confirmed", actions);
        Assert.Contains("Revoked", actions);
        var confirmedEvent = row.GetProperty("events").EnumerateArray().Single(e => e.GetProperty("action").GetString() == "Confirmed");
        Assert.Contains("data-processing@1.0-borrador", confirmedEvent.GetProperty("documentVersions").GetString());
    }

    [Fact]
    public async Task Existing_students_declare_once_and_other_roles_cannot()
    {
        var student = _api.ClientFor(_api.Users.StudentA);
        var before = await Json(await student.GetAsync("/api/legal/me/age"));
        Assert.True(before.GetProperty("required").GetBoolean());
        Assert.False(before.GetProperty("declared").GetBoolean());

        var adult = await Json(await student.PostAsJsonAsync("/api/legal/me/age", new { birthDate = "1990-01-15" }));
        Assert.True(adult.GetProperty("declared").GetBoolean());
        Assert.False(adult.GetProperty("isMinor").GetBoolean());

        await AssertError(await student.PostAsJsonAsync("/api/legal/me/age", new { birthDate = "2012-01-15" }),
            HttpStatusCode.Conflict, "age_already_declared");

        var teacher = _api.ClientFor(_api.Users.Teacher1);
        var teacherAge = await Json(await teacher.GetAsync("/api/legal/me/age"));
        Assert.False(teacherAge.GetProperty("required").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await teacher.PostAsJsonAsync("/api/legal/me/age", new { birthDate = "1990-01-15" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/admin/legal/minors")).StatusCode);
    }
}
