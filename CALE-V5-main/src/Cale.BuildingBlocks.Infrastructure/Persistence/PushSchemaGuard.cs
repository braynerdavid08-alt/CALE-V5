using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent creation of the Web Push tables (device subscriptions and the VAPID key pair).
/// Runs even when FeatureSchema is disabled.
/// </summary>
public static class PushSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "SuscripcionesPush" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "Endpoint" varchar(1000) NOT NULL,
            "P256dh" varchar(200) NOT NULL,
            "Auth" varchar(100) NOT NULL,
            "UserAgent" varchar(300) NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "LastSeenAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SuscripcionesPush_Endpoint" ON "SuscripcionesPush" ("Endpoint");""",
        """CREATE INDEX IF NOT EXISTS "IX_SuscripcionesPush_UserId" ON "SuscripcionesPush" ("UserId");""",
        """
        CREATE TABLE IF NOT EXISTS "ClavesPush" (
            "Id" integer NOT NULL PRIMARY KEY,
            "PublicKey" varchar(200) NOT NULL,
            "PrivateKey" varchar(200) NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "SuscripcionesPush" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "Endpoint" TEXT NOT NULL,
            "P256dh" TEXT NOT NULL,
            "Auth" TEXT NOT NULL,
            "UserAgent" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "LastSeenAt" TEXT NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SuscripcionesPush_Endpoint" ON "SuscripcionesPush" ("Endpoint");""",
        """CREATE INDEX IF NOT EXISTS "IX_SuscripcionesPush_UserId" ON "SuscripcionesPush" ("UserId");""",
        """
        CREATE TABLE IF NOT EXISTS "ClavesPush" (
            "Id" INTEGER NOT NULL PRIMARY KEY,
            "PublicKey" TEXT NOT NULL,
            "PrivateKey" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """
    ];

    public static async Task EnsureAsync(
        CaleDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
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
                logger?.LogWarning(ex, "PushSchemaGuard statement failed; push notifications may be unavailable.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("PushSchemaGuard applied (SuscripcionesPush/ClavesPush).");
        }
    }
}
