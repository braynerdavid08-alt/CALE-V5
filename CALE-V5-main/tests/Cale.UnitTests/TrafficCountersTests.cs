using Cale.Api.Middleware;

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
}
