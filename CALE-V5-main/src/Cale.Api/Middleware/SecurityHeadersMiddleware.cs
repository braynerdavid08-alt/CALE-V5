namespace Cale.Api.Middleware;

/// <summary>
/// Browser hardening headers for the SPA and the API. The CSP allows only our own scripts, so an
/// injected &lt;script&gt; or inline handler never runs. Exceptions, and why:
/// - style-src 'unsafe-inline': Angular injects component styles as &lt;style&gt; elements.
/// - fonts.googleapis.com / fonts.gstatic.com: the DM Sans font.
/// - img-src https: teachers and the admin may reference external images.
/// - frame-src YouTube: lesson video blocks; frame-ancestors 'self': the live host embeds our own presentation page.
/// Security:Csp:Mode = enforce (default) | report-only | off, to roll back without a deploy if something breaks.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _mode;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration, IWebHostEnvironment env)
    {
        _next = next;
        var fallback = env.IsDevelopment() ? "off" : "enforce";
        _mode = (configuration["Security:Csp:Mode"] ?? fallback).Trim().ToLowerInvariant();
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "SAMEORIGIN";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        if (context.Request.IsHttps)
        {
            headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
        }

        if (_mode != "off")
        {
            var policy = BuildPolicy(context.Request.Host.Value ?? "");
            headers[_mode == "report-only" ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy"] = policy;
        }

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.OnStarting(static state =>
            {
                var response = (HttpResponse)state;
                if (string.IsNullOrEmpty(response.Headers.CacheControl))
                {
                    response.Headers.CacheControl = "no-store";
                    response.Headers.Pragma = "no-cache";
                }

                return Task.CompletedTask;
            }, context.Response);
        }

        return _next(context);
    }

    public static string BuildPolicy(string host) =>
        string.Join("; ",
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src 'self' data: https://fonts.gstatic.com",
            "img-src 'self' data: blob: https:",
            "media-src 'self' blob: data:",
            $"connect-src 'self' wss://{host} ws://{host}",
            "frame-src 'self' https://www.youtube.com https://www.youtube-nocookie.com",
            "worker-src 'self'",
            "manifest-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'self'");
}
