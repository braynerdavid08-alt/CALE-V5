using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Features;

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
            var originalBody = context.Features.Get<IHttpResponseBodyFeature>();
            var countingBody = originalBody is null ? null : new CountingResponseBodyFeature(originalBody);
            if (countingBody is not null)
            {
                context.Features.Set<IHttpResponseBodyFeature>(countingBody);
            }

            try
            {
                await _next(context);
                if (countingBody is not null)
                {
                    await countingBody.CompleteAsync();
                }
            }
            finally
            {
                sw.Stop();
                if (originalBody is not null)
                {
                    context.Features.Set(originalBody);
                }

                var status = context.Response.StatusCode;
                long? bytes = countingBody is not null ? countingBody.Counter.BytesWritten : context.Response.ContentLength;
                Traffic.Record(TrafficCategory(path), status, bytes, RouteKey(path));
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

    private static readonly Regex IdSegment = new(
        "^([0-9]+|[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}|[A-Za-z0-9_\\-\\.]{24,})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Low-cardinality route key for the traffic summary: ids and tokens become {id}, static assets
    /// collapse by extension, course videos keep their (fixed) file name. Query strings are dropped.
    /// </summary>
    public static string RouteKey(string path)
    {
        var category = TrafficCategory(path);
        if (category == "videos")
        {
            return path.ToLowerInvariant();
        }

        if (category == "static")
        {
            var ext = Path.GetExtension(path);
            return string.IsNullOrEmpty(ext) ? "static (spa)" : "static *" + ext.ToLowerInvariant();
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (IdSegment.IsMatch(segments[i]))
            {
                segments[i] = "{id}";
            }
        }

        return "/" + string.Join('/', segments).ToLowerInvariant();
    }

    public static string GetRequestId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id && !string.IsNullOrWhiteSpace(id)
            ? id
            : context.TraceIdentifier;
}

/// <summary>
/// In-memory request/byte counters per traffic category and per route, flushed to log lines per
/// window. Bytes are what the app wrote to the body after WireEncryption's own gzip; text assets
/// that ResponseCompression (outer middleware) compresses are counted before compression.
/// </summary>
public sealed class TrafficCounters
{
    public const int MaxRoutes = 400;
    public const int TopRoutes = 10;
    private const string OverflowRoute = "(otras rutas)";

    private readonly TimeSpan _window;
    private readonly object _gate = new();
    private Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private Dictionary<string, Bucket> _routes = new(StringComparer.Ordinal);
    private DateTime _since = DateTime.UtcNow;

    public TrafficCounters(TimeSpan window) => _window = window;

    public void Record(string category, int status, long? bytes, string? route = null)
    {
        lock (_gate)
        {
            Add(_buckets, category, status, bytes);
            if (route is not null)
            {
                var key = _routes.Count >= MaxRoutes && !_routes.ContainsKey(route) ? OverflowRoute : route;
                Add(_routes, key, status, bytes);
            }
        }
    }

    private static void Add(Dictionary<string, Bucket> map, string key, int status, long? bytes)
    {
        if (!map.TryGetValue(key, out var b))
        {
            b = new Bucket();
            map[key] = b;
        }

        b.Requests++;
        b.Bytes += bytes ?? 0;
        if (status == StatusCodes.Status304NotModified) b.NotModified++;
        if (status == StatusCodes.Status206PartialContent) b.Partial++;
    }

    public bool TryTakeSummary(DateTime utcNow, out (double Minutes, string Text) summary)
    {
        summary = default;
        Dictionary<string, Bucket> taken;
        Dictionary<string, Bucket> routes;
        DateTime since;
        lock (_gate)
        {
            if (utcNow - _since < _window)
            {
                return false;
            }

            taken = _buckets;
            routes = _routes;
            since = _since;
            _buckets = new(StringComparer.Ordinal);
            _routes = new(StringComparer.Ordinal);
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
        if (routes.Count > 0)
        {
            text += " | top rutas por bytes: " + string.Join(
                "; ",
                routes.OrderByDescending(kv => kv.Value.Bytes)
                    .ThenByDescending(kv => kv.Value.Requests)
                    .Take(TopRoutes)
                    .Select(kv => FormattableString.Invariant(
                        $"{kv.Key} {kv.Value.Requests} req {kv.Value.Bytes / 1024d / 1024d:F2} MB")));
            text += " | top rutas por peticiones: " + string.Join(
                "; ",
                routes.OrderByDescending(kv => kv.Value.Requests)
                    .Take(TopRoutes)
                    .Select(kv => FormattableString.Invariant($"{kv.Key} {kv.Value.Requests} req")));
        }

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
