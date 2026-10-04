using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent creation of the account suspension history table. Runs even when FeatureSchema is disabled.
/// </summary>
public static class AccountStatusSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "HistorialEstadoCuenta" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "Action" varchar(20) NOT NULL,
            "Reason" varchar(500) NOT NULL,
            "Evidence" varchar(1000) NULL,
            "SuspendedUntil" timestamp with time zone NULL,
            "ActorUserId" integer NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_HistorialEstadoCuenta_UserId_CreatedAt" ON "HistorialEstadoCuenta" ("UserId", "CreatedAt");"""
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "HistorialEstadoCuenta" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "Action" TEXT NOT NULL,
            "Reason" TEXT NOT NULL,
            "Evidence" TEXT NULL,
            "SuspendedUntil" TEXT NULL,
            "ActorUserId" INTEGER NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_HistorialEstadoCuenta_UserId_CreatedAt" ON "HistorialEstadoCuenta" ("UserId", "CreatedAt");"""
    ];

    public static async Task EnsureAsync(CaleDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        string[] statements;
        if (db.Database.IsNpgsql())
        {
            statements = PostgresStatements;
        }
        else if (db.Database.IsSqlite())
        {
            statements = SqliteStatements;
        }
        else
        {
            return;
        }

        var failures = 0;
        foreach (var sql in statements)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch (Exception ex)
            {
                failures++;
                logger?.LogWarning(ex, "AccountStatusSchemaGuard statement failed; suspension history may be unavailable.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("AccountStatusSchemaGuard applied (HistorialEstadoCuenta).");
        }
    }
}
