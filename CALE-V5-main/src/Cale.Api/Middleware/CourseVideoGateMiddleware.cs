using Cale.BuildingBlocks.Domain.Auth;
using Microsoft.AspNetCore.Authentication;

namespace Cale.Api.Middleware;

/// <summary>
/// Private files that live in wwwroot. Without this gate anyone with the link could download them.
/// The browser sends the HttpOnly access cookie with &lt;video&gt; and link requests, so the session is checked here.
/// Course videos: any signed-in user. Payment receipts: admins and schools only.
/// </summary>
public sealed class CourseVideoGateMiddleware
{
    private static readonly PathString Videos = new("/courses/videos");
    private static readonly PathString Receipts = new("/uploads/receipts");
    private readonly RequestDelegate _next;

    public CourseVideoGateMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var isVideo = path.StartsWithSegments(Videos);
        var isReceipt = path.StartsWithSegments(Receipts);
        if (!isVideo && !isReceipt)
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

        if (isReceipt && !(auth.Principal.IsInRole(Roles.Admin) || auth.Principal.IsInRole(Roles.School)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.ContentDisposition = isReceipt ? "attachment" : "inline";
            context.Response.Headers["X-Robots-Tag"] = "noindex";
            return Task.CompletedTask;
        });
        await _next(context);
    }
}
