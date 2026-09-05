using System.Security.Claims;
using Cale.Modules.Identity.Application.Abstractions;

namespace Cale.Api.Middleware;

/// <summary>
/// Blocks authenticated API use until forced password change is completed.
/// </summary>
public sealed class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IUserStore users)
    {
        var path = context.Request.Path.Value ?? "";
        if (IsExempt(path) || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var idRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idRaw, out var userId) || userId <= 0)
        {
            await _next(context);
            return;
        }

        var user = await users.GetByIdAsync(userId, context.RequestAborted);
        if (user is { MustChangePassword: true })
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Debes cambiar tu contraseña temporal antes de continuar.",
                detail = "password_change_required",
                status = 403
            });
            return;
        }

        await _next(context);
    }

    private static bool IsExempt(string path)
    {
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/public", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // SignalR negotiate/negotiate cookies still need auth but hub is separate map.
        return path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase);
    }
}
