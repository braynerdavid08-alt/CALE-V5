using System.Globalization;
using System.Text;
using Cale.BuildingBlocks.Infrastructure.Diagnostics;

namespace Cale.Api.Services.Diagnostics;

/// <summary>
/// Every 15 minutes logs how much the app moved to/from the database and to external services,
/// so Render's "Service-Initiated" traffic can be compared against what the code actually did.
/// No SQL, payloads, recipients or connection strings are logged.
/// </summary>
public sealed class ResourceUsageSummaryService : BackgroundService
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly DatabaseTrafficMonitor _database;
    private readonly ILogger<ResourceUsageSummaryService> _logger;

    public ResourceUsageSummaryService(DatabaseTrafficMonitor database, ILogger<ResourceUsageSummaryService> logger)
    {
        _database = database;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _database.TakeSnapshot();
        using var timer = new PeriodicTimer(Window);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation("Database traffic summary ({Minutes} min): {Summary}",
                        Window.TotalMinutes, FormatDatabase(_database.TakeSnapshot()));
                    _logger.LogInformation("Outbound traffic summary ({Minutes} min): {Summary}",
                        Window.TotalMinutes, FormatOutbound(OutboundTraffic.TakeSnapshot()));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Resource usage summary failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public static string FormatDatabase(DatabaseTrafficSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"read={Mb(snapshot.BytesRead)} MB written={Mb(snapshot.BytesWritten)} MB");
        foreach (var (name, value) in snapshot.Values)
        {
            if (name.EndsWith("bytes_read", StringComparison.Ordinal) || name.EndsWith("bytes_written", StringComparison.Ordinal))
            {
                continue;
            }

            sb.Append(CultureInfo.InvariantCulture, $" {name}={Math.Round(value, 2)}");
        }

        return sb.ToString();
    }

    public static string FormatOutbound(IReadOnlyDictionary<string, OutboundTargetStats> stats)
    {
        if (stats.Count == 0)
        {
            return "none";
        }

        return string.Join("; ", stats
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => FormattableString.Invariant(
                $"{kv.Key} calls={kv.Value.Calls} failed={kv.Value.Failures} sent={Kb(kv.Value.SentBytes)} KB received={Kb(kv.Value.ReceivedBytes)} KB")));
    }

    private static string Mb(long bytes) => (bytes / 1024d / 1024d).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Kb(long bytes) => (bytes / 1024d).ToString("0.0", CultureInfo.InvariantCulture);
}
