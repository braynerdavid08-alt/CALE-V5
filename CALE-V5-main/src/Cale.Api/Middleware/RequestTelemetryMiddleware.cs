using System.Diagnostics;
using System.Security.Claims;

namespace Cale.Api.Middleware;

public sealed class RequestTelemetryMiddleware
{
    public const string HeaderName = "X-Request-Id";
    public const string ItemKey = "Cale.RequestId";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTelemetryMiddleware> _logger;

    public RequestTelemetryMiddleware(
        RequestDelegate next,
        ILogger<RequestTelemetryMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = requestId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["Path"] = path,
            ["Method"] = method
        }))
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                var status = context.Response.StatusCode;
                var bytes = context.Response.ContentLength;
                Traffic.Record(TrafficCategory(path), status, bytes);
                if (!path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase))
                {
                    // User is available after authentication middleware has run.
                    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "-";
                    _logger.LogInformation(
                        "HTTP {Method} {Path} => {StatusCode} in {ElapsedMs}ms Bytes={Bytes} RequestId={RequestId} UserId={UserId}",
                        method,
                        path,
                        status,
                        sw.ElapsedMilliseconds,
                        bytes?.ToString() ?? "-",
                        requestId,
                        userId);
                }

                if (Traffic.TryTakeSummary(DateTime.UtcNow, out var summary))
                {
                    _logger.LogInformation("HTTP traffic summary (last {Minutes:F0} min): {Summary}", summary.Minutes, summary.Text);
                }
            }
        }
    }

    internal static readonly TrafficCounters Traffic = new(TimeSpan.FromMinutes(15));

    public static string TrafficCategory(string path)
    {
        if (path.StartsWith("/courses/videos", StringComparison.OrdinalIgnoreCase)) return "videos";
        if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase)) return "signalr";
        if (path.Contains("/media/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return "media";
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return "api";
        return "static";
    }

    public static string GetRequestId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id && !string.IsNullOrWhiteSpace(id)
            ? id
            : context.TraceIdentifier;
}

/// <summary>
/// In-memory request/byte counters per traffic category, flushed to one log line per window.
/// Bytes only count responses with a known Content-Length (videos, files, media); compressed JSON
/// is streamed without one, so API rows mainly show request counts.
/// </summary>
public sealed class TrafficCounters
{
    private readonly TimeSpan _window;
    private readonly object _gate = new();
    private Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private DateTime _since = DateTime.UtcNow;

    public TrafficCounters(TimeSpan window) => _window = window;

    public void Record(string category, int status, long? bytes)
    {
        lock (_gate)
        {
            if (!_buckets.TryGetValue(category, out var b))
            {
                b = new Bucket();
                _buckets[category] = b;
            }

            b.Requests++;
            b.Bytes += bytes ?? 0;
            if (status == StatusCodes.Status304NotModified) b.NotModified++;
            if (status == StatusCodes.Status206PartialContent) b.Partial++;
        }
    }

    public bool TryTakeSummary(DateTime utcNow, out (double Minutes, string Text) summary)
    {
        summary = default;
        Dictionary<string, Bucket> taken;
        DateTime since;
        lock (_gate)
        {
            if (utcNow - _since < _window)
            {
                return false;
            }

            taken = _buckets;
            since = _since;
            _buckets = new(StringComparer.Ordinal);
            _since = utcNow;
        }

        if (taken.Count == 0)
        {
            return false;
        }

        var text = string.Join(
            "; ",
            taken.OrderByDescending(kv => kv.Value.Bytes).Select(kv => FormattableString.Invariant(
                $"{kv.Key}: {kv.Value.Requests} req, {kv.Value.Bytes / 1024d / 1024d:F1} MB, {kv.Value.Partial} parciales(206), {kv.Value.NotModified} sin cambios(304)")));
        summary = ((utcNow - since).TotalMinutes, text);
        return true;
    }

    private sealed class Bucket
    {
        public long Requests;
        public long Bytes;
        public long NotModified;
        public long Partial;
    }
}
