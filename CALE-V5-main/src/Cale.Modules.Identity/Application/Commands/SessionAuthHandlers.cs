using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Security;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Microsoft.Extensions.Options;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class IssueAuthSessionHandler
{
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IClock _clock;
    private readonly JwtOptions _jwt;

    public IssueAuthSessionHandler(
        ITokenService tokens,
        IRefreshTokenStore refreshTokens,
        IClock clock,
        IOptions<JwtOptions> jwt)
    {
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _clock = clock;
        _jwt = jwt.Value;
    }

    public async Task<AuthSessionResult> IssueAsync(
        int userId,
        string email,
        string name,
        string role,
        bool mustChangePassword,
        CancellationToken ct)
    {
        var normalized = Roles.Normalize(role);
        var access = _tokens.Create(userId, email, name, normalized);
        var refreshDays = _jwt.RefreshTokenDays > 0 ? _jwt.RefreshTokenDays : 14;
        var refresh = await _refreshTokens.IssueAsync(
            userId,
            _clock.UtcNow.AddDays(refreshDays),
            ct);

        return new AuthSessionResult(
            access,
            refresh,
            new AuthResponse(
                string.Empty,
                userId,
                name,
                email,
                normalized,
                mustChangePassword,
                UsesCookieAuth: true));
    }
}

public sealed record AuthSessionResult(
    string AccessToken,
    string RefreshToken,
    AuthResponse Response);

public sealed class RefreshAuthSessionHandler
{
    private readonly IUserStore _users;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IssueAuthSessionHandler _issue;

    public RefreshAuthSessionHandler(
        IUserStore users,
        IRefreshTokenStore refreshTokens,
        IssueAuthSessionHandler issue)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _issue = issue;
    }

    public async Task<AuthSessionResult> HandleAsync(
        string refreshToken,
        CancellationToken ct)
    {
        var userId = await _refreshTokens.ConsumeAsync(refreshToken, ct)
            ?? throw new UnauthorizedException("Invalid refresh token.", "invalid_refresh");

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found.", "invalid_refresh");

        if (!user.IsActive)
        {
            throw new ForbiddenException("User is inactive.", "user_inactive");
        }

        return await _issue.IssueAsync(
            user.Id,
            user.Email,
            user.Name,
            user.Role,
            user.MustChangePassword,
            ct);
    }
}

public sealed class LogoutAuthSessionHandler
{
    private readonly IRefreshTokenStore _refreshTokens;

    public LogoutAuthSessionHandler(IRefreshTokenStore refreshTokens) =>
        _refreshTokens = refreshTokens;

    public Task HandleAsync(string? refreshToken, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(refreshToken)
            ? Task.CompletedTask
            : _refreshTokens.RevokeAsync(refreshToken, ct);
}
