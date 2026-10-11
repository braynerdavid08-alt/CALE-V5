using System.Data;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.LiveClassroom.Infrastructure.Persistence;

/// <summary>
/// Idempotent "LiveSessions"."QuestionClosedAt" column (production runs with EF migrations off).
/// Existing rows get NULL, so each stale expired question is closed once by the timer and then
/// never selected again.
/// </summary>
public static class LiveSessionSchemaGuard
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
                await db.Database.ExecuteSqlRawAsync(
                    """
                    ALTER TABLE IF EXISTS "LiveSessions"
                    ADD COLUMN IF NOT EXISTS "QuestionClosedAt" timestamp with time zone NULL;
                    """,
                    ct);
            }
            else if (db.Database.IsSqlServer())
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    IF OBJECT_ID(N'dbo.LiveSessions', N'U') IS NOT NULL
                       AND COL_LENGTH(N'dbo.LiveSessions', N'QuestionClosedAt') IS NULL
                        ALTER TABLE dbo.LiveSessions ADD QuestionClosedAt datetime2 NULL;
                    """,
                    ct);
            }
            else if (db.Database.IsSqlite())
            {
                if (await ScalarAsync(db, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'LiveSessions';", ct) == 0)
                {
                    return;
                }

                if (await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_table_info('LiveSessions') WHERE name = 'QuestionClosedAt';", ct) == 0)
                {
                    await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "LiveSessions" ADD COLUMN "QuestionClosedAt" TEXT NULL;""", ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "LiveSessionSchemaGuard failed; the live question timer may not work.");
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
