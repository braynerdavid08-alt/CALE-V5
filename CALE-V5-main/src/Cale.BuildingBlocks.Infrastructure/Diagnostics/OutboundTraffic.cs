namespace Cale.BuildingBlocks.Infrastructure.Diagnostics;

/// <summary>
/// Process-wide counters for calls the app starts towards external services (LLM, Web Push,
/// SMTP). Only counts and payload sizes are kept: never content, recipients or keys.
/// </summary>
public static class OutboundTraffic
{
    private static readonly object Gate = new();
    private static Dictionary<string, OutboundTargetStats> _stats = new(StringComparer.Ordinal);

    public static void Record(string target, long sentBytes, long receivedBytes, bool failed = false)
    {
        lock (Gate)
        {
            if (!_stats.TryGetValue(target, out var s))
            {
                s = new OutboundTargetStats();
                _stats[target] = s;
            }

            s.Calls++;
            s.SentBytes += Math.Max(0, sentBytes);
            s.ReceivedBytes += Math.Max(0, receivedBytes);
            if (failed)
            {
                s.Failures++;
            }
        }
    }

    /// <summary>Returns the totals since the previous call and starts a new window.</summary>
    public static IReadOnlyDictionary<string, OutboundTargetStats> TakeSnapshot()
    {
        lock (Gate)
        {
            var snapshot = _stats;
            _stats = new Dictionary<string, OutboundTargetStats>(StringComparer.Ordinal);
            return snapshot;
        }
    }
}

public sealed class OutboundTargetStats
{
    public long Calls { get; internal set; }
    public long Failures { get; internal set; }
    public long SentBytes { get; internal set; }
    public long ReceivedBytes { get; internal set; }
}
