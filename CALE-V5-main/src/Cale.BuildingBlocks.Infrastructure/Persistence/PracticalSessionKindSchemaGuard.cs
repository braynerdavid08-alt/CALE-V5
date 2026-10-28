using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent Kind column on PracticalLessonSessions (Lesson | Exam).
/// </summary>
public static class PracticalSessionKindSchemaGuard
{
    public const string PostgresStatement =
        """
        ALTER TABLE "PracticalLessonSessions"
          ADD COLUMN IF NOT EXISTS "Kind" varchar(16) NOT NULL DEFAULT 'Lesson';
        UPDATE "PracticalLessonSessions" SET "Capacity" = 1 WHERE "Capacity" <> 1;
        """;

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
                await db.Database.ExecuteSqlRawAsync(PostgresStatement, ct);
            }
            else if (db.Database.IsSqlite())
            {
                if (await ScalarAsync(
                        db,
                        "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'PracticalLessonSessions';",
                        ct) == 0)
                {
                    return;
                }

                var exists = await ScalarAsync(
                    db,
                    "SELECT COUNT(*) FROM pragma_table_info('PracticalLessonSessions') WHERE name = 'Kind';",
                    ct);
                if (exists == 0)
                {
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PracticalLessonSessions" ADD COLUMN "Kind" TEXT NOT NULL DEFAULT 'Lesson';""",
                        ct);
                }

                await db.Database.ExecuteSqlRawAsync(
                    """UPDATE "PracticalLessonSessions" SET "Capacity" = 1 WHERE "Capacity" <> 1;""",
                    ct);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "PracticalSessionKindSchemaGuard failed; exam slots may be unavailable.");
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
