using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.Modules.Identity.Domain;
using Xunit;

namespace Cale.UnitTests.Security.Integration;

public sealed class PqrApiTests : IAsyncLifetime
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

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task Success(HttpResponseMessage response) =>
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anyone_can_file_without_login_and_follow_it_with_the_tracking_code()
    {
        var anon = _api.ClientFor(null);
        await AssertError(await anon.PostAsJsonAsync("/api/pqr", new
        {
            kind = "supresion", name = "Persona Externa", email = "no-es-correo", subject = "Borrar mis datos",
            message = "Quiero que eliminen todos mis datos personales de la plataforma."
        }), HttpStatusCode.BadRequest, "pqr_email_invalid");

        await AssertError(await anon.PostAsJsonAsync("/api/pqr", new
        {
            kind = "inventado", name = "Persona Externa", email = "externa@gmail.com", subject = "Asunto", message = "Mensaje"
        }), HttpStatusCode.BadRequest, "pqr_kind_invalid");

        var created = await Json(await anon.PostAsJsonAsync("/api/pqr", new
        {
            kind = "supresion", name = "Persona Externa", email = "externa@gmail.com", subject = "Borrar mis datos",
            message = "Quiero que eliminen todos mis datos personales de la plataforma."
        }));
        Assert.StartsWith("PQR-", created.GetProperty("radicado").GetString());
        var code = created.GetProperty("trackingCode").GetString()!;

        var tracked = await Json(await anon.GetAsync($"/api/pqr/track/{code}"));
        Assert.Equal("PENDIENTE", tracked.GetProperty("status").GetString());
        Assert.Equal("supresion", tracked.GetProperty("kind").GetString());
        await AssertError(await anon.GetAsync("/api/pqr/track/ABCDEF"), HttpStatusCode.NotFound, "pqr_not_found");

        var admin = _api.ClientFor(_api.Users.Admin);
        var list = await Json(await admin.GetAsync("/api/admin/pqr?status=open"));
        var row = list.EnumerateArray().Single(r => r.GetProperty("contactEmail").GetString() == "externa@gmail.com");
        Assert.True(row.GetProperty("isDataRight").GetBoolean());
        var id = row.GetProperty("id").GetInt32();

        await AssertError(await admin.PutAsJsonAsync($"/api/admin/pqr/{id}", new { status = "RECHAZADA", response = "" }),
            HttpStatusCode.BadRequest, "pqr_response_required");

        await Success(await admin.PutAsJsonAsync($"/api/admin/pqr/{id}", new
        {
            status = "EN_REVISION", internalNote = "Verificar identidad antes de borrar."
        }));
        await Success(await admin.PostAsJsonAsync($"/api/admin/pqr/{id}/extend", new { reason = "Necesitamos verificar la identidad del titular." }));
        await AssertError(await admin.PostAsJsonAsync($"/api/admin/pqr/{id}/extend", new { reason = "Otra prórroga más larga" }),
            HttpStatusCode.Conflict, "pqr_already_extended");
        await Success(await admin.PutAsJsonAsync($"/api/admin/pqr/{id}", new
        {
            status = "RECHAZADA", response = "No pudimos verificar que el correo pertenezca al titular de la cuenta."
        }));
        await AssertError(await admin.PutAsJsonAsync($"/api/admin/pqr/{id}", new { status = "EN_REVISION" }),
            HttpStatusCode.Conflict, "pqr_closed");

        var closed = await Json(await anon.GetAsync($"/api/pqr/track/{code}"));
        Assert.Equal("RECHAZADA", closed.GetProperty("status").GetString());
        Assert.True(closed.GetProperty("extended").GetBoolean());
        Assert.Contains("No pudimos verificar", closed.GetProperty("response").GetString());
        Assert.DoesNotContain("Verificar identidad", closed.GetRawText());

        var adminView = await Json(await admin.GetAsync("/api/admin/pqr"));
        var history = adminView.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id).GetProperty("events");
        Assert.Contains(history.EnumerateArray(), e => e.GetProperty("action").GetString() == "Note");
    }

    [Fact]
    public async Task Logged_in_users_see_only_their_own_requests_and_only_admins_manage_them()
    {
        var student = _api.ClientFor(_api.Users.StudentA);
        await Json(await student.PostAsJsonAsync("/api/pqr", new
        {
            kind = "consulta", subject = "Qué datos tienen", message = "Quiero saber qué datos personales míos guardan y para qué."
        }));
        var mine = await Json(await student.GetAsync("/api/pqr/mine"));
        Assert.Equal(1, mine.GetArrayLength());

        var other = _api.ClientFor(_api.Users.StudentB);
        Assert.Equal(0, (await Json(await other.GetAsync("/api/pqr/mine"))).GetArrayLength());

        var teacher = _api.ClientFor(_api.Users.Teacher1);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/admin/pqr")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await teacher.PutAsJsonAsync("/api/admin/pqr/1", new { status = "RESPONDIDA", response = "Respuesta indebida" })).StatusCode);

        var admin = _api.ClientFor(_api.Users.Admin);
        var row = (await Json(await admin.GetAsync("/api/admin/pqr"))).EnumerateArray()
            .Single(r => r.GetProperty("userId").ValueKind == JsonValueKind.Number
                && r.GetProperty("userId").GetInt32() == _api.Users.StudentA.Id);
        Assert.Equal(_api.Users.StudentA.Email, row.GetProperty("contactEmail").GetString());
    }

    [Fact]
    public void Deadlines_count_business_days_in_colombia()
    {
        // Viernes 9 de octubre de 2026, 10:00 en Colombia.
        var friday = new DateTime(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);
        var due = PqrDeadlines.AddBusinessDays(friday, PqrDeadlines.ConsultaDays);
        Assert.Equal(new DateTime(2026, 10, 23), due.AddHours(-5).Date);
    }
}
