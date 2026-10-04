using Cale.Api.Extensions;

namespace Cale.Api.Middleware;

/// <summary>
/// The session lives in cookies, so a state-changing /api request is only accepted when the browser says it
/// comes from our own site (Sec-Fetch-Site / Origin) or from a configured CORS origin. Requests without those
/// headers (cURL, server-to-server) carry no ambient cookies from a victim, so they are not CSRF and pass through.
/// Bearer-token requests are never sent automatically by a browser and are exempt as well.
/// </summary>
public sealed class CsrfOriginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _allowed;
    private readonly ILogger<CsrfOriginMiddleware> _logger;

    public CsrfOriginMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<CsrfOriginMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _allowed = ServiceCollectionExtensions.ResolveCorsOrigins(configuration)
            .Where(o => o != "*")
            .Select(o => o.TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!IsUnsafe(request.Method)
            || !request.Path.StartsWithSegments("/api")
            || request.Headers.Authorization.Count > 0)
        {
            return _next(context);
        }

        var origin = request.Headers.Origin.ToString();
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        var self = $"{request.Scheme}://{request.Host}";

        var originOk = string.IsNullOrEmpty(origin)
            || string.Equals(origin, self, StringComparison.OrdinalIgnoreCase)
            || _allowed.Contains(origin);
        var siteOk = !string.Equals(fetchSite, "cross-site", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(origin) && _allowed.Contains(origin));

        if (originOk && siteOk)
        {
            return _next(context);
        }

        _logger.LogWarning(
            "Blocked cross-site {Method} {Path} (origin {Origin}, fetch-site {FetchSite}).",
            request.Method,
            request.Path.Value,
            string.IsNullOrEmpty(origin) ? "-" : origin,
            string.IsNullOrEmpty(fetchSite) ? "-" : fetchSite);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(
            new { title = "Forbidden", status = 403, code = "csrf_origin_rejected" },
            (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json");
    }

    private static bool IsUnsafe(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method)
        || HttpMethods.IsDelete(method);
}
