using System.Data;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.LiveClassroom.Infrastructure.Persistence;

/// <summary>
/// Idempotent LiveSessions columns (production runs with EF migrations off).
/// QuestionClosedAt: NULL on existing rows so each stale expired question closes once.
/// CurrentSlideIndex: presentation follow index survives API restarts.
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
                    ALTER TABLE IF EXISTS "LiveSessions"
                    ADD COLUMN IF NOT EXISTS "CurrentSlideIndex" integer NOT NULL DEFAULT 0;
                    ALTER TABLE IF EXISTS "LiveSessions"
                    ADD COLUMN IF NOT EXISTS "RemoteCode" varchar(12) NOT NULL DEFAULT '';
                    CREATE INDEX IF NOT EXISTS "IX_LiveSessions_RemoteCode" ON "LiveSessions" ("RemoteCode");
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
                    IF OBJECT_ID(N'dbo.LiveSessions', N'U') IS NOT NULL
                       AND COL_LENGTH(N'dbo.LiveSessions', N'CurrentSlideIndex') IS NULL
                        ALTER TABLE dbo.LiveSessions ADD CurrentSlideIndex int NOT NULL
                            CONSTRAINT DF_LiveSessions_CurrentSlideIndex DEFAULT(0);
                    IF OBJECT_ID(N'dbo.LiveSessions', N'U') IS NOT NULL
                       AND COL_LENGTH(N'dbo.LiveSessions', N'RemoteCode') IS NULL
                        ALTER TABLE dbo.LiveSessions ADD RemoteCode nvarchar(12) NOT NULL
                            CONSTRAINT DF_LiveSessions_RemoteCode DEFAULT(N'');
                    IF OBJECT_ID(N'dbo.LiveSessions', N'U') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1 FROM sys.indexes
                           WHERE name = N'IX_LiveSessions_RemoteCode' AND object_id = OBJECT_ID(N'dbo.LiveSessions'))
                        CREATE INDEX IX_LiveSessions_RemoteCode ON dbo.LiveSessions (RemoteCode);
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

                if (await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_table_info('LiveSessions') WHERE name = 'CurrentSlideIndex';", ct) == 0)
                {
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "LiveSessions" ADD COLUMN "CurrentSlideIndex" INTEGER NOT NULL DEFAULT 0;""",
                        ct);
                }

                if (await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_table_info('LiveSessions') WHERE name = 'RemoteCode';", ct) == 0)
                {
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "LiveSessions" ADD COLUMN "RemoteCode" TEXT NOT NULL DEFAULT '';""",
                        ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "LiveSessionSchemaGuard failed; live timer or slide sync may not work.");
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
