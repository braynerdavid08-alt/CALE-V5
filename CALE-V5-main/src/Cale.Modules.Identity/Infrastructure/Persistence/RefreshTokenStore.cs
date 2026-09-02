using System.Security.Cryptography;
using System.Text;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly CaleDbContext _db;

    public RefreshTokenStore(CaleDbContext db) => _db = db;

    public async Task<string> IssueAsync(
        int userId,
        DateTime expiresAtUtc,
        CancellationToken ct = default)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hash = Hash(raw);
        var created = DateTime.UtcNow;

        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "AuthRefreshTokens" ("UserId", "TokenHash", "ExpiresAt", "CreatedAt")
                VALUES ({userId}, {hash}, {expiresAtUtc}, {created});
                """,
                ct);
        }
        else
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "AuthRefreshTokens" ("UserId", "TokenHash", "ExpiresAt", "CreatedAt")
                VALUES ({userId}, {hash}, {expiresAtUtc}, {created});
                """,
                ct);
        }

        return raw;
    }

    public async Task<int?> ConsumeAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var hash = Hash(rawToken);
        var now = DateTime.UtcNow;

        await using var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT "Id", "UserId", "ExpiresAt", "RevokedAt"
            FROM "AuthRefreshTokens"
            WHERE "TokenHash" = @hash
            LIMIT 1;
            """;
        var p = cmd.CreateParameter();
        p.ParameterName = "@hash";
        p.Value = hash;
        cmd.Parameters.Add(p);

        int? tokenId = null;
        int? userId = null;
        DateTime? expiresAt = null;
        DateTime? revokedAt = null;

        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            tokenId = reader.GetInt32(0);
            userId = reader.GetInt32(1);
            expiresAt = reader.GetDateTime(2);
            revokedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
        }

        if (tokenId is null || userId is null || revokedAt is not null || expiresAt <= now)
        {
            return null;
        }

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "AuthRefreshTokens" SET "RevokedAt" = {now} WHERE "Id" = {tokenId};""",
            ct);

        return userId;
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var hash = Hash(rawToken);
        var now = DateTime.UtcNow;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "AuthRefreshTokens" SET "RevokedAt" = {now} WHERE "TokenHash" = {hash} AND "RevokedAt" IS NULL;""",
            ct);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "AuthRefreshTokens" SET "RevokedAt" = {now} WHERE "UserId" = {userId} AND "RevokedAt" IS NULL;""",
            ct);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
