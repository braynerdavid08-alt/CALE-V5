using Microsoft.AspNetCore.Authentication;

namespace Cale.Api.Middleware;

/// <summary>
/// Course videos live in wwwroot, so without this gate anyone with the link could download them.
/// The browser sends the HttpOnly access cookie with &lt;video&gt; requests, so only signed-in users get them.
/// </summary>
public sealed class CourseVideoGateMiddleware
{
    private static readonly PathString Prefix = new("/courses/videos");
    private readonly RequestDelegate _next;

    public CourseVideoGateMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(Prefix))
        {
            await _next(context);
            return;
        }

        var auth = await context.AuthenticateAsync();
        if (!auth.Succeeded)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.ContentDisposition = "inline";
            context.Response.Headers["X-Robots-Tag"] = "noindex";
            return Task.CompletedTask;
        });
        await _next(context);
    }
}
