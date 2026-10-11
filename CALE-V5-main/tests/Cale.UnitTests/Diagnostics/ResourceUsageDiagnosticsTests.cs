using System.Diagnostics.Metrics;
using Cale.Api.Services.Diagnostics;
using Cale.BuildingBlocks.Infrastructure.Diagnostics;
using Xunit;

namespace Cale.UnitTests.Diagnostics;

public class ResourceUsageDiagnosticsTests
{
    [Fact]
    public void Database_monitor_reports_cumulative_counters_as_window_deltas()
    {
        var meterName = "Test.Npgsql." + Guid.NewGuid().ToString("N");
        long read = 1000, written = 200;
        using var meter = new Meter(meterName);
        meter.CreateObservableCounter("db.client.commands.bytes_read",
            () => new Measurement<long>(read, new KeyValuePair<string, object?>("pool", "a")));
        meter.CreateObservableCounter("db.client.commands.bytes_written",
            () => new Measurement<long>(written, new KeyValuePair<string, object?>("pool", "a")));
        using var monitor = new DatabaseTrafficMonitor(meterName);

        var first = monitor.TakeSnapshot();
        Assert.Equal(1000, first.BytesRead);
        Assert.Equal(200, first.BytesWritten);

        read = 4000;
        written = 250;
        var second = monitor.TakeSnapshot();
        Assert.Equal(3000, second.BytesRead);
        Assert.Equal(50, second.BytesWritten);

        var idle = monitor.TakeSnapshot();
        Assert.Equal(0, idle.BytesRead);
    }

    [Fact]
    public void Database_monitor_counts_histogram_events_and_resets_counters_per_window()
    {
        var meterName = "Test.Npgsql." + Guid.NewGuid().ToString("N");
        using var meter = new Meter(meterName);
        var duration = meter.CreateHistogram<double>("db.client.commands.duration");
        var failed = meter.CreateCounter<int>("db.client.commands.failed");
        var executing = meter.CreateUpDownCounter<int>("db.client.commands.executing");
        using var monitor = new DatabaseTrafficMonitor(meterName);

        duration.Record(0.004);
        duration.Record(0.020);
        duration.Record(1.5);
        failed.Add(1);
        executing.Add(1);
        executing.Add(1);
        executing.Add(-1);

        var snapshot = monitor.TakeSnapshot();
        Assert.Equal(3, snapshot.Values["db.client.commands.duration"]);
        Assert.Equal(1, snapshot.Values["db.client.commands.failed"]);
        Assert.Equal(1, snapshot.Values["db.client.commands.executing"]);

        var next = monitor.TakeSnapshot();
        Assert.Equal(0, next.Values["db.client.commands.duration"]);
        Assert.Equal(0, next.Values["db.client.commands.failed"]);
        Assert.Equal(1, next.Values["db.client.commands.executing"]);
    }

    [Fact]
    public void Database_monitor_ignores_other_meters()
    {
        var meterName = "Test.Npgsql." + Guid.NewGuid().ToString("N");
        using var other = new Meter("Test.Other." + Guid.NewGuid().ToString("N"));
        var counter = other.CreateCounter<long>("db.client.commands.bytes_read");
        using var monitor = new DatabaseTrafficMonitor(meterName);

        counter.Add(999);

        Assert.Empty(monitor.TakeSnapshot().Values);
    }

    [Fact]
    public void Outbound_traffic_aggregates_per_target_and_starts_a_new_window()
    {
        var target = "test-" + Guid.NewGuid().ToString("N");
        OutboundTraffic.Record(target, 1500, 300);
        OutboundTraffic.Record(target, 500, 0, failed: true);

        var snapshot = OutboundTraffic.TakeSnapshot();

        var stats = snapshot[target];
        Assert.Equal(2, stats.Calls);
        Assert.Equal(1, stats.Failures);
        Assert.Equal(2000, stats.SentBytes);
        Assert.Equal(300, stats.ReceivedBytes);
        Assert.False(OutboundTraffic.TakeSnapshot().ContainsKey(target));
    }

    [Fact]
    public void Summaries_use_invariant_format_and_never_include_tags()
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-CO");
            var db = new DatabaseTrafficSnapshot(new Dictionary<string, double>
            {
                ["db.client.commands.bytes_read"] = 3 * 1024 * 1024 + 512 * 1024,
                ["db.client.commands.bytes_written"] = 1024 * 1024,
                ["db.client.commands.duration"] = 42
            });

            var dbText = ResourceUsageSummaryService.FormatDatabase(db);
            Assert.Equal("read=3.50 MB written=1.00 MB db.client.commands.duration=42", dbText);

            var outbound = new Dictionary<string, OutboundTargetStats>();
            Assert.Equal("none", ResourceUsageSummaryService.FormatOutbound(outbound));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }
}
