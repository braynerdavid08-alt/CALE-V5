using System.Net;
using System.Net.Http.Json;

namespace Cale.UnitTests.Security.Integration;

/// <summary>Own host so exhausting the login limiter never affects the attack-matrix tests.</summary>
public sealed class RateLimitTests : IDisposable
{
    private readonly SecurityApiFactory _api = new();

    [Fact]
    public async Task Brute_force_login_gets_429()
    {
        var client = _api.ClientFor(null);
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            var res = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email = "nadie@pruebas.test", password = "incorrecta-" + i });
            statuses.Add(res.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.OK, statuses);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    public void Dispose() => _api.Dispose();
}
