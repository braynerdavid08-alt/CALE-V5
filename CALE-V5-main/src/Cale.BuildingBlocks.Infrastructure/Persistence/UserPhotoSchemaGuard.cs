using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent profile-photo column on "Usuarios". Runs even when FeatureSchema is disabled.
/// Mirrors migration <c>AddUserPhoto</c>.
/// </summary>
public static class UserPhotoSchemaGuard
{
    public const string PostgresStatement =
        """ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "FotoUrl" varchar(300) NULL;""";

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
                if (await ScalarAsync(db, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Usuarios';", ct) == 0)
                {
                    return;
                }

                var exists = await ScalarAsync(
                    db,
                    "SELECT COUNT(*) FROM pragma_table_info('Usuarios') WHERE name = 'FotoUrl';",
                    ct);
                if (exists == 0)
                {
                    await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Usuarios" ADD COLUMN "FotoUrl" TEXT NULL;""", ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "UserPhotoSchemaGuard failed; profile photos may be unavailable.");
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
