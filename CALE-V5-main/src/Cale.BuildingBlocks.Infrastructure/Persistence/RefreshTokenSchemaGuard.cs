using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Adds AuthRefreshTokens.RotatedAt / GraceUses, used to cap refresh reuse inside the rotation grace
/// window and to detect a rotated token replayed afterwards. Runs even when FeatureSchema is off.
/// </summary>
public static class RefreshTokenSchemaGuard
{
    public static async Task EnsureAsync(CaleDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        try
        {
            if (db.Database.IsNpgsql())
            {
                if (await ScalarAsync(db, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'AuthRefreshTokens';", ct) == 0)
                {
                    return;
                }

                await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "AuthRefreshTokens" ADD COLUMN IF NOT EXISTS "RotatedAt" timestamp with time zone NULL;""", ct);
                await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "AuthRefreshTokens" ADD COLUMN IF NOT EXISTS "GraceUses" integer NOT NULL DEFAULT 0;""", ct);
            }
            else if (db.Database.IsSqlite())
            {
                if (await ScalarAsync(db, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AuthRefreshTokens';", ct) == 0)
                {
                    return;
                }

                if (await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_table_info('AuthRefreshTokens') WHERE name = 'RotatedAt';", ct) == 0)
                {
                    await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "AuthRefreshTokens" ADD COLUMN "RotatedAt" TEXT NULL;""", ct);
                }

                if (await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_table_info('AuthRefreshTokens') WHERE name = 'GraceUses';", ct) == 0)
                {
                    await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "AuthRefreshTokens" ADD COLUMN "GraceUses" INTEGER NOT NULL DEFAULT 0;""", ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "RefreshTokenSchemaGuard failed; refresh falls back to the legacy consume until it succeeds.");
        }
    }

    public static async Task<bool> HasReuseColumnsAsync(CaleDbContext db, CancellationToken ct)
    {
        var sql = db.Database.IsNpgsql()
            ? "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'AuthRefreshTokens' AND column_name IN ('RotatedAt', 'GraceUses');"
            : db.Database.IsSqlite()
                ? "SELECT COUNT(*) FROM pragma_table_info('AuthRefreshTokens') WHERE name IN ('RotatedAt', 'GraceUses');"
                : null;
        return sql is not null && await ScalarAsync(db, sql, ct) == 2;
    }

    private static async Task<long> ScalarAsync(CaleDbContext db, string sql, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result ?? 0);
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }
}
