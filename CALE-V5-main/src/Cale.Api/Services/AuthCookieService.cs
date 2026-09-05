using Cale.BuildingBlocks.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Cale.Api.Services;

public static class AuthCookieNames
{
    public const string Access = "cale_access";
    public const string Refresh = "cale_refresh";
}

public sealed class AuthCookieService
{
    private readonly JwtOptions _jwt;
    private readonly IWebHostEnvironment _env;

    public AuthCookieService(IOptions<JwtOptions> jwt, IWebHostEnvironment env)
    {
        _jwt = jwt.Value;
        _env = env;
    }

    public void Set(HttpResponse response, string accessToken, string refreshToken)
    {
        var secure = !_env.IsDevelopment();
        var accessMinutes = _jwt.AccessTokenMinutes > 0
            ? _jwt.AccessTokenMinutes
            : Math.Max(15, _jwt.ExpirationHours * 60);
        var refreshDays = _jwt.RefreshTokenDays > 0 ? _jwt.RefreshTokenDays : 14;

        response.Cookies.Append(AuthCookieNames.Access, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(accessMinutes)
        });

        response.Cookies.Append(AuthCookieNames.Refresh, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(refreshDays)
        });
    }

    public void Clear(HttpResponse response)
    {
        var secure = !_env.IsDevelopment();
        response.Cookies.Delete(AuthCookieNames.Access, new CookieOptions
        {
            Path = "/",
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true
        });
        response.Cookies.Delete(AuthCookieNames.Refresh, new CookieOptions
        {
            Path = "/api/auth",
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            HttpOnly = true
        });
    }
}