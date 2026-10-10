using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Cale.UnitTests.Security.Integration;

public sealed class LegalAcceptanceApiTests : IAsyncLifetime
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

    private static object AllConsent(bool school = false) => new
    {
        acceptTerms = true,
        acknowledgePrivacy = true,
        authorizeDataProcessing = true,
        acceptSchoolTerms = school
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] Statuses(JsonElement status) =>
        status.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("status").GetString()!).ToArray();

    [Fact]
    public async Task Initial_documents_are_published_as_drafts_with_placeholders()
    {
        var anon = _api.ClientFor(null);
        var list = await Json(await anon.GetAsync("/api/legal/documents"));
        Assert.Equal(5, list.GetArrayLength());
        Assert.All(list.EnumerateArray(), d => Assert.True(d.GetProperty("isDraft").GetBoolean()));

        var terms = await Json(await anon.GetAsync("/api/legal/documents/terms"));
        Assert.Equal("1.0-borrador", terms.GetProperty("version").GetString());
        Assert.Contains("BORRADOR", terms.GetProperty("content").GetString());
        Assert.Contains("[NOMBRE LEGAL - PENDIENTE]", terms.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Registration_requires_each_checkbox_and_stores_the_evidence()
    {
        var anon = _api.ClientFor(null);
        var missing = await anon.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Nueva Estudiante",
            email = "luzverde.prueba.legal@gmail.com",
            password = "Prueba-Segura-2026!",
            consent = new { acceptTerms = true, acknowledgePrivacy = true, authorizeDataProcessing = false }
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("legal_consent_required", await missing.Content.ReadAsStringAsync());

        var created = await Json(await anon.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Nueva Estudiante",
            email = "luzverde.prueba.legal@gmail.com",
            password = "Prueba-Segura-2026!",
            consent = AllConsent(),
            birthDate = "1995-04-20"
        }));

        var client = _api.ClientFor(null);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", created.GetProperty("token").GetString());
        var status = await Json(await client.GetAsync("/api/legal/me"));
        Assert.True(status.GetProperty("allAccepted").GetBoolean());

        var history = await Json(await client.GetAsync("/api/legal/me/history"));
        Assert.Equal(3, history.GetArrayLength());
        Assert.All(history.EnumerateArray(), h => Assert.Equal("Registration", h.GetProperty("method").GetString()));
    }

    [Fact]
    public async Task Existing_users_start_not_verified_and_a_new_required_version_marks_them_outdated()
    {
        var student = _api.ClientFor(_api.Users.StudentA);
        var before = await Json(await student.GetAsync("/api/legal/me"));
        Assert.False(before.GetProperty("allAccepted").GetBoolean());
        Assert.All(Statuses(before), s => Assert.Equal("NOT_VERIFIED", s));

        var partial = await student.PostAsJsonAsync("/api/legal/me/accept", new { acceptTerms = true });
        Assert.Equal(HttpStatusCode.BadRequest, partial.StatusCode);

        var accepted = await Json(await student.PostAsJsonAsync("/api/legal/me/accept", AllConsent()));
        Assert.True(accepted.GetProperty("allAccepted").GetBoolean());

        var admin = _api.ClientFor(_api.Users.Admin);
        var draft = await Json(await admin.PostAsJsonAsync("/api/admin/legal/versions", new
        {
            documentKey = "terms",
            version = "1.1-borrador",
            title = "Términos y Condiciones",
            content = "# Términos\n\nTexto actualizado. [CORREO LEGAL - PENDIENTE]",
            changeSummary = "Cambio relevante",
            requiresReacceptance = true
        }));
        Assert.True(draft.GetProperty("hasPlaceholders").GetBoolean());
        var id = draft.GetProperty("id").GetInt32();
        await Json(await admin.PostAsync($"/api/admin/legal/versions/{id}/publish", null));

        var locked = await admin.PutAsJsonAsync($"/api/admin/legal/versions/{id}", new
        {
            documentKey = "terms",
            version = "1.1-borrador",
            title = "Términos",
            content = "Edición silenciosa",
            requiresReacceptance = false
        });
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);

        var after = await Json(await student.GetAsync("/api/legal/me"));
        var terms = after.GetProperty("documents").EnumerateArray().Single(d => d.GetProperty("key").GetString() == "terms");
        Assert.Equal("OUTDATED", terms.GetProperty("status").GetString());
        Assert.Equal("1.1-borrador", terms.GetProperty("currentVersion").GetString());

        var csv = await admin.GetAsync("/api/admin/legal/acceptances.csv");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Contains("student.a@pruebas.test", await csv.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Only_admins_manage_legal_versions()
    {
        var teacher = _api.ClientFor(_api.Users.Teacher1);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/admin/legal/summary")).StatusCode);
        var create = await teacher.PostAsJsonAsync("/api/admin/legal/versions", new
        {
            documentKey = "terms",
            version = "9.9",
            title = "x",
            content = "x",
            requiresReacceptance = false
        });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }
}
