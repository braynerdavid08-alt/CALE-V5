using System.Net;
using System.Net.Http.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Security.Integration;

/// <summary>
/// Refresh-token rotation and reuse, cross-site request forgery, answer-key leaks during an open exam,
/// push SSRF and deactivated accounts, all through the real HTTP pipeline.
/// </summary>
[Collection(SecurityApiCollection.Name)]
public sealed class SessionAndRequestForgeryTests(SecurityApiFixture fixture)
{
    private readonly SecurityApiFactory _api = fixture.Factory;
    private SecurityApiFactory.Accounts U => _api.Users;
    private SecurityApiFactory.Fixtures D => _api.Data;

    // ---------- Refresh tokens ----------

    [Fact]
    public async Task Refresh_rotates_and_the_new_cookie_works()
    {
        var first = await LoginRefreshCookieAsync(U.Teacher1.Email);
        var (status, second) = await RefreshAsync(first);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);

        var (again, _) = await RefreshAsync(second!);
        Assert.Equal(HttpStatusCode.OK, again);
    }

    [Fact]
    public async Task Rotated_refresh_is_only_accepted_a_couple_of_times_inside_the_grace_window()
    {
        var token = await LoginRefreshCookieAsync(U.Teacher1.Email);
        var results = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            results.Add((await RefreshAsync(token)).Status);
        }

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Unauthorized],
            results);
    }

    [Fact]
    public async Task Replaying_a_rotated_refresh_after_the_grace_window_revokes_every_session_of_that_user()
    {
        var stolen = await LoginRefreshCookieAsync(U.Teacher2.Email);
        var (_, successor) = await RefreshAsync(stolen);
        Assert.NotNull(successor);

        await AgeRotatedTokensAsync(U.Teacher2.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(stolen)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(successor!)).Status);
    }

    [Fact]
    public async Task Refresh_after_logout_is_401()
    {
        var token = await LoginRefreshCookieAsync(U.Teacher1.Email);
        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"cale_refresh={token}");
        Assert.Equal(HttpStatusCode.NoContent, (await _api.ClientFor(null).SendAsync(logout)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(token)).Status);
    }

    [Fact]
    public async Task Garbage_refresh_cookie_is_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync("bm90LWEtcmVhbC10b2tlbg")).Status);
    }

    // ---------- Cross-site request forgery ----------

    [Fact]
    public async Task Cross_origin_state_change_with_cookies_is_rejected()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        req.Headers.Add("Origin", "https://evil.example");
        var res = await _api.ClientFor(null).SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Cross_site_fetch_metadata_is_rejected_even_without_origin()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        req.Headers.Add("Sec-Fetch-Site", "cross-site");
        var res = await _api.ClientFor(null).SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Same_origin_state_change_is_allowed()
    {
        var client = _api.ClientFor(null);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        req.Headers.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
        req.Headers.Add("Sec-Fetch-Site", "same-origin");
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Cross_site_reads_are_not_blocked()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        req.Headers.Add("Origin", "https://evil.example");
        req.Headers.Authorization = new("Bearer", _api.TokenFor(U.StudentA));
        var res = await _api.ClientFor(null).SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- Answer keys during an open exam ----------

    [Fact]
    public async Task Practice_mistakes_never_reveal_the_key_of_a_question_in_an_open_exam()
    {
        var res = await _api.ClientFor(U.StudentA).PostAsJsonAsync(
            "/api/student/play/mistakes/answer",
            new { questionId = D.PrivateQuestionId, optionId = 0 });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("correct", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Solucionario privado", body);
    }

    // ---------- Misc ----------

    [Fact]
    public async Task Push_subscription_to_an_internal_host_is_rejected()
    {
        var res = await _api.ClientFor(U.StudentA).PostAsJsonAsync(
            "/api/push/subscriptions",
            new { endpoint = "https://10.0.0.5:8443/internal", keys = new { p256dh = "x", auth = "y" } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Valid_token_of_a_deactivated_user_is_401()
    {
        var res = await _api.ClientFor(U.InactiveStudent).GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_route_is_a_json_problem()
    {
        var res = await _api.ClientFor(null).PostAsync("/api/no-existe/26", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    // ---------- helpers ----------

    private async Task<string> LoginRefreshCookieAsync(string email)
    {
        var res = await _api.ClientFor(null).PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = SecurityApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return RefreshCookie(res) ?? throw new InvalidOperationException("Login did not set the refresh cookie.");
    }

    private async Task<(HttpStatusCode Status, string? NewToken)> RefreshAsync(string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Add("Cookie", $"cale_refresh={token}");
        var res = await _api.ClientFor(null).SendAsync(req);
        return (res.StatusCode, RefreshCookie(res));
    }

    private static string? RefreshCookie(HttpResponseMessage res) =>
        res.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies
                .Where(c => c.StartsWith("cale_refresh=", StringComparison.Ordinal))
                .Select(c => c["cale_refresh=".Length..].Split(';')[0])
                .FirstOrDefault(v => v.Length > 0)
            : null;

    private async Task AgeRotatedTokensAsync(int userId)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var past = DateTime.UtcNow.AddMinutes(-10);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "AuthRefreshTokens" SET "RevokedAt" = {past}, "RotatedAt" = {past} WHERE "UserId" = {userId} AND "RotatedAt" IS NOT NULL;""");
    }
}
