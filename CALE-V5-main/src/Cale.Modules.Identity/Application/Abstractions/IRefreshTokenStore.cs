namespace Cale.Modules.Identity.Application.Abstractions;

public interface IRefreshTokenStore
{
    Task<string> IssueAsync(int userId, DateTime expiresAtUtc, CancellationToken ct = default);

    Task<int?> ConsumeAsync(string rawToken, CancellationToken ct = default);

    Task RevokeAsync(string rawToken, CancellationToken ct = default);

    Task RevokeAllForUserAsync(int userId, CancellationToken ct = default);
}
