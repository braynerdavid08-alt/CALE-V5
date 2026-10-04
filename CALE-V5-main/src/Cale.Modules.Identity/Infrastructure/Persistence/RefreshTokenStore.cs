using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class RefreshTokenStore : IRefreshTokenStore
{
    /// <summary>
    /// A token rotated this recently can still be consumed a couple more times: two tabs (or a retry after a
    /// dropped response) often refresh with the same cookie at the same moment, and the loser must not be logged out.
    /// </summary>
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(60);

    /// <summary>Extra consumes allowed for one token inside <see cref="RotationGrace"/>.</summary>
    private const int MaxGraceUses = 2;

    /// <summary>Explicit revocations are back-dated past the grace window so a logged-out token is never reusable.</summary>
    private static readonly TimeSpan RevocationBackdate = TimeSpan.FromDays(1);

    private static volatile bool _reuseColumnsReady;

    private readonly CaleDbContext _db;
    private readonly ILogger<RefreshTokenStore> _logger;

    public RefreshTokenStore(CaleDbContext db, ILogger<RefreshTokenStore>? logger = null)
    {
        _db = db;
        _logger = logger ?? NullLogger<RefreshTokenStore>.Instance;
    }

    public async Task<string> IssueAsync(
        int userId,
        DateTime expiresAtUtc,
        CancellationToken ct = default)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hash = Hash(raw);
        var created = DateTime.UtcNow;

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "AuthRefreshTokens" ("UserId", "TokenHash", "ExpiresAt", "CreatedAt")
            VALUES ({userId}, {hash}, {expiresAtUtc}, {created});
            """,
            ct);

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
        var graceStart = now - RotationGrace;

        // The connection belongs to the scoped DbContext: open/close it through EF, never dispose it,
        // or later queries in the same request fail with ObjectDisposedException.
        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            var tracked = _reuseColumnsReady
                || (_reuseColumnsReady = await RefreshTokenSchemaGuard.HasReuseColumnsAsync(_db, ct));

            var userId = await ScalarIntAsync(ConsumeSql(tracked), hash, now, graceStart, ct);
            if (userId is not null || !tracked)
            {
                return userId;
            }

            // A token rotated before the grace window is being replayed: either the owner or a thief
            // already holds its successor. Revoke the whole session family for that user.
            var reusedBy = await ScalarIntAsync(ReuseSql(), hash, now, graceStart, ct);
            if (reusedBy is int victim)
            {
                await RevokeAllForUserAsync(victim, ct);
                _logger.LogWarning(
                    "Refresh token reuse detected for user {UserId}; all sessions revoked.",
                    victim);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbException ex)
        {
            _logger.LogError(ex, "Refresh token consume failed.");
            return null;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var hash = Hash(rawToken);
        var revokedAt = DateTime.UtcNow - RevocationBackdate;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "AuthRefreshTokens" SET "RevokedAt" = {revokedAt} WHERE "TokenHash" = {hash} AND ("RevokedAt" IS NULL OR "RevokedAt" > {revokedAt});""",
            ct);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken ct = default)
    {
        var revokedAt = DateTime.UtcNow - RevocationBackdate;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "AuthRefreshTokens" SET "RevokedAt" = {revokedAt} WHERE "UserId" = {userId} AND ("RevokedAt" IS NULL OR "RevokedAt" > {revokedAt});""",
            ct);
    }

    private string ConsumeSql(bool tracked)
    {
        // Atomic consume; the first revocation time is kept so the grace window never extends.
        var set = tracked
            ? """
              "RevokedAt" = COALESCE("RevokedAt", @now),
              "RotatedAt" = COALESCE("RotatedAt", @now),
              "GraceUses" = CASE WHEN "RevokedAt" IS NULL THEN "GraceUses" ELSE "GraceUses" + 1 END
              """
            : "\"RevokedAt\" = COALESCE(\"RevokedAt\", @now)";
        var match = tracked
            ? $"""
              "TokenHash" = @hash
              AND ("RevokedAt" IS NULL OR ("RevokedAt" > @graceStart AND "GraceUses" < {MaxGraceUses}))
              AND "ExpiresAt" > @now
              """
            : """
              "TokenHash" = @hash
              AND ("RevokedAt" IS NULL OR "RevokedAt" > @graceStart)
              AND "ExpiresAt" > @now
              """;

        if (_db.Database.IsSqlite())
        {
            return $"""
                UPDATE "AuthRefreshTokens" SET {set}
                WHERE "Id" = (SELECT "Id" FROM "AuthRefreshTokens" WHERE {match} LIMIT 1)
                RETURNING "UserId";
                """;
        }

        if (_db.Database.IsNpgsql())
        {
            return $"""UPDATE "AuthRefreshTokens" SET {set} WHERE {match} RETURNING "UserId";""";
        }

        return $"""UPDATE "AuthRefreshTokens" SET {set} OUTPUT INSERTED."UserId" WHERE {match};""";
    }

    // Clears RotatedAt on the replayed row so the same stale cookie can't keep revoking fresh logins.
    private string ReuseSql() =>
        _db.Database.IsSqlite() || _db.Database.IsNpgsql()
            ? """
              UPDATE "AuthRefreshTokens" SET "RotatedAt" = NULL
              WHERE "TokenHash" = @hash AND "RotatedAt" IS NOT NULL AND "RotatedAt" <= @graceStart AND "ExpiresAt" > @now
              RETURNING "UserId";
              """
            : """
              UPDATE "AuthRefreshTokens" SET "RotatedAt" = NULL OUTPUT INSERTED."UserId"
              WHERE "TokenHash" = @hash AND "RotatedAt" IS NOT NULL AND "RotatedAt" <= @graceStart AND "ExpiresAt" > @now;
              """;

    private async Task<int?> ScalarIntAsync(
        string sql,
        string hash,
        DateTime now,
        DateTime graceStart,
        CancellationToken ct)
    {
        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "@hash", hash);
        AddParameter(cmd, "@now", now);
        AddParameter(cmd, "@graceStart", graceStart);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static void AddParameter(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
