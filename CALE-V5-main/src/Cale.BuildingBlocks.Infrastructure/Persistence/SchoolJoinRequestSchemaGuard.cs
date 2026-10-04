using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Adds SchoolJoinRequests.Direction (member request vs. school invitation). Runs even when
/// FeatureSchema is off; existing rows are member requests.
/// </summary>
public static class SchoolJoinRequestSchemaGuard
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
                if (await ScalarAsync(db, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'SchoolJoinRequests';", ct) == 0)
                {
                    return;
                }

                await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "SchoolJoinRequests" ADD COLUMN IF NOT EXISTS "Direction" character varying(16) NOT NULL DEFAULT 'Request';""", ct);
            }
            else if (db.Database.IsSqlite())
            {
                if (await ScalarAsync(db, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'SchoolJoinRequests';", ct) == 0)
                {
                    return;
                }

                if (await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_table_info('SchoolJoinRequests') WHERE name = 'Direction';", ct) == 0)
                {
                    await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "SchoolJoinRequests" ADD COLUMN "Direction" TEXT NOT NULL DEFAULT 'Request';""", ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "SchoolJoinRequestSchemaGuard failed; school invitations are unavailable until it succeeds.");
        }
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
