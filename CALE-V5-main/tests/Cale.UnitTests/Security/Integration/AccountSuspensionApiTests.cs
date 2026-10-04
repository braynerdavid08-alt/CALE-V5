using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Cale.UnitTests.Security.Integration;

public sealed class AccountSuspensionApiTests
{
    [Fact]
    public async Task Admin_suspends_with_reason_and_the_user_is_told_why_at_login()
    {
        using var api = new SecurityApiFactory();
        _ = api.Server;
        await api.SeedAsync();
        var target = api.Users.LoneTeacher;
        var admin = api.ClientFor(api.Users.Admin);

        var noReason = await admin.PatchAsJsonAsync($"/api/admin/users/{target.Id}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var suspended = await admin.PatchAsJsonAsync(
            $"/api/admin/users/{target.Id}/active",
            new { isActive = false, reason = "Publicó contenido ofensivo en el aula.", evidence = "Reporte #12", durationDays = 7 });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);

        var login = await api.ClientFor(null).PostAsJsonAsync(
            "/api/auth/login",
            new { email = target.Email, password = SecurityApiFactory.Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        using var problem = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.Equal("account_suspended", problem.RootElement.GetProperty("detail").GetString());
        Assert.Contains("Publicó contenido ofensivo en el aula.", problem.RootElement.GetProperty("title").GetString());

        var history = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/users/{target.Id}/status-history");
        Assert.Equal(1, history.GetArrayLength());
        Assert.Equal("Reporte #12", history[0].GetProperty("evidence").GetString());
        Assert.Equal("Admin_Global", history[0].GetProperty("actorName").GetString());

        var asTeacher = await api.ClientFor(api.Users.Teacher1).GetAsync($"/api/admin/users/{target.Id}/status-history");
        Assert.Equal(HttpStatusCode.Forbidden, asTeacher.StatusCode);
    }
}
