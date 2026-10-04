using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Adds Usuarios.EscuelaCreadoraId. When the column is first created, existing school members are
/// marked as created by their school unless the membership log shows the school only linked them.
/// </summary>
public static class UserCreatorSchemaGuard
{
    private const string Backfill =
        """
        UPDATE "Usuarios" SET "EscuelaCreadoraId" = "SchoolId"
        WHERE "SchoolId" IS NOT NULL
          AND NOT EXISTS (
            SELECT 1 FROM "MembershipEvents" e
            WHERE e."SchoolUserId" = "Usuarios"."SchoolId"
              AND e."EventType" = 'MemberAttached'
              AND e."Note" LIKE '%(#' || CAST("Usuarios"."Id" AS TEXT) || ')%');
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
                var exists = await ScalarAsync(
                    db,
                    "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'Usuarios' AND column_name = 'EscuelaCreadoraId';",
                    ct);
                if (exists == 0)
                {
                    await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "EscuelaCreadoraId" integer NULL;""", ct);
                    await db.Database.ExecuteSqlRawAsync(Backfill, ct);
                }
            }
            else if (db.Database.IsSqlite())
            {
                if (await ScalarAsync(db, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Usuarios';", ct) == 0)
                {
                    return;
                }

                var exists = await ScalarAsync(
                    db,
                    "SELECT COUNT(*) FROM pragma_table_info('Usuarios') WHERE name = 'EscuelaCreadoraId';",
                    ct);
                if (exists == 0)
                {
                    await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Usuarios" ADD COLUMN "EscuelaCreadoraId" INTEGER NULL;""", ct);
                    await db.Database.ExecuteSqlRawAsync(Backfill, ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "UserCreatorSchemaGuard failed; schools cannot reset member passwords until it succeeds.");
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
