using Cale.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace Cale.UnitTests;

public sealed class TrafficCountersTests
{
    [Theory]
    [InlineData("/courses/videos/clase.mp4", "videos")]
    [InlineData("/hubs/live", "signalr")]
    [InlineData("/api/media/0f8fad5b-d9cb-469f-a165-70867728950e", "media")]
    [InlineData("/api/auth/access-mode", "api")]
    [InlineData("/chunk-ABC.js", "static")]
    public void Paths_are_grouped_without_ids(string path, string expected) =>
        Assert.Equal(expected, RequestTelemetryMiddleware.TrafficCategory(path));

    [Fact]
    public void Summary_is_emitted_once_per_window_and_then_resets()
    {
        var counters = new TrafficCounters(TimeSpan.FromMinutes(15));
        var start = DateTime.UtcNow;
        counters.Record("videos", 206, 10 * 1024 * 1024);
        counters.Record("videos", 304, null);
        counters.Record("api", 200, null);

        Assert.False(counters.TryTakeSummary(start.AddMinutes(1), out _));
        Assert.True(counters.TryTakeSummary(start.AddMinutes(16), out var summary));
        Assert.Contains("videos: 2 req, 10.0 MB, 1 parciales(206), 1 sin cambios(304)", summary.Text);
        Assert.Contains("api: 1 req", summary.Text);

        Assert.False(counters.TryTakeSummary(start.AddMinutes(40), out _));
    }

    [Theory]
    [InlineData("/api/media/0f8fad5b-d9cb-469f-a165-70867728950e", "/api/media/{id}")]
    [InlineData("/api/live/sessions/42/state", "/api/live/sessions/{id}/state")]
    [InlineData("/api/game-show/rooms/0f8fad5bd9cb469fa16570867728950e/play", "/api/game-show/rooms/{id}/play")]
    [InlineData("/api/Auth/Me", "/api/auth/me")]
    [InlineData("/courses/videos/Clase-1.mp4", "/courses/videos/clase-1.mp4")]
    [InlineData("/chunk-ABC123.js", "static *.js")]
    [InlineData("/cursos/senales", "static (spa)")]
    [InlineData("/hubs/live", "/hubs/live")]
    public void Route_keys_collapse_ids_and_assets(string path, string expected) =>
        Assert.Equal(expected, RequestTelemetryMiddleware.RouteKey(path));

    [Fact]
    public void Summary_lists_top_routes_and_caps_distinct_routes()
    {
        var counters = new TrafficCounters(TimeSpan.FromMinutes(15));
        var start = DateTime.UtcNow;
        counters.Record("api", 200, 5 * 1024 * 1024, "/api/big");
        counters.Record("api", 200, 100, "/api/small");
        counters.Record("api", 200, 100, "/api/small");
        for (var i = 0; i < TrafficCounters.MaxRoutes + 50; i++)
        {
            counters.Record("api", 200, 1, "/api/r" + i);
        }

        Assert.True(counters.TryTakeSummary(start.AddMinutes(16), out var summary));
        Assert.Contains("top rutas por bytes: /api/big 1 req 5.00 MB", summary.Text);
        Assert.Contains("(otras rutas)", summary.Text);
    }

    [Fact]
    public async Task Middleware_counts_bytes_written_without_content_length()
    {
        var middleware = new RequestTelemetryMiddleware(
            async ctx =>
            {
                await ctx.Response.WriteAsync(new string('x', 3000));
                await ctx.Response.BodyWriter.WriteAsync(new byte[500]);
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RequestTelemetryMiddleware>.Instance);
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var sink = new MemoryStream();
        context.Response.Body = sink;
        context.Request.Path = "/api/telemetry-test-" + Guid.NewGuid().ToString("N");
        var original = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();

        await middleware.InvokeAsync(context);

        Assert.Equal(3500, sink.Length);
        Assert.Same(original, context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>());
    }
}
