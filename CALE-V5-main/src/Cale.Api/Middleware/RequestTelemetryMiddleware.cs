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
                if (!path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase))
                {
                    // User is available after authentication middleware has run.
                    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "-";
                    _logger.LogInformation(
                        "HTTP {Method} {Path} => {StatusCode} in {ElapsedMs}ms RequestId={RequestId} UserId={UserId}",
                        method,
                        path,
                        status,
                        sw.ElapsedMilliseconds,
                        requestId,
                        userId);
                }
            }
        }
    }

    public static string GetRequestId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id && !string.IsNullOrWhiteSpace(id)
            ? id
            : context.TraceIdentifier;
}
