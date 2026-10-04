using System.Security.Claims;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Cale.Api.Middleware;

/// <summary>
/// Re-checks the account behind every authenticated API call: a deleted, deactivated or
/// re-roled user loses access immediately instead of when the access token expires.
/// Also blocks API use until a forced password change is completed.
/// </summary>
public sealed class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MustChangePasswordMiddleware> _logger;

    public MustChangePasswordMiddleware(RequestDelegate next, ILogger<MustChangePasswordMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserStore users)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            || context.User.Identity?.IsAuthenticated != true
            || context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        var idRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idRaw, out var userId) || userId <= 0)
        {
            await RejectAsync(context, 401, "Tu sesión no es válida. Inicia sesión de nuevo.", "session_invalid");
            return;
        }

        var user = await users.GetByIdAsync(userId, context.RequestAborted);
        var tokenRole = Roles.Normalize(context.User.FindFirstValue(ClaimTypes.Role) ?? "");
        if (user is null || !user.IsActive || Roles.Normalize(user.Role) != tokenRole)
        {
            _logger.LogWarning(
                "Security: rejected token for userId={UserId} (exists={Exists}, active={Active}, roleChanged={RoleChanged}) path={Path}",
                userId,
                user is not null,
                user?.IsActive,
                user is not null && Roles.Normalize(user.Role) != tokenRole,
                path);
            await RejectAsync(context, 401, "Tu sesión ya no es válida. Inicia sesión de nuevo.", "session_revoked");
            return;
        }

        if (user.MustChangePassword && !IsPasswordChangeExempt(path))
        {
            await RejectAsync(context, 403, "Debes cambiar tu contraseña temporal antes de continuar.", "password_change_required");
            return;
        }

        await _next(context);
    }

    private static Task RejectAsync(HttpContext context, int status, string title, string code)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsJsonAsync(new { title, detail = code, status });
    }

    private static bool IsPasswordChangeExempt(string path) =>
        path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/public", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase);
}
