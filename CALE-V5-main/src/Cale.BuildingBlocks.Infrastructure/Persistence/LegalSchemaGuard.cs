using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent creation of the legal document versions and acceptance evidence tables.
/// Runs even when FeatureSchema is disabled.
/// </summary>
public static class LegalSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "LegalVersionesDocumento" (
            "Id" serial PRIMARY KEY,
            "DocumentKey" varchar(40) NOT NULL,
            "Version" varchar(40) NOT NULL,
            "Title" varchar(200) NOT NULL,
            "Content" text NOT NULL,
            "ContentHash" varchar(64) NOT NULL,
            "Status" varchar(20) NOT NULL,
            "RequiresReacceptance" boolean NOT NULL DEFAULT true,
            "ChangeSummary" varchar(500) NULL,
            "CreatedByUserId" integer NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "PublishedByUserId" integer NULL,
            "PublishedAt" timestamp with time zone NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LegalVersionesDocumento_DocumentKey_Version" ON "LegalVersionesDocumento" ("DocumentKey", "Version");""",
        """
        CREATE TABLE IF NOT EXISTS "LegalAceptaciones" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "DocumentKey" varchar(40) NOT NULL,
            "DocumentVersionId" integer NOT NULL,
            "Version" varchar(40) NOT NULL,
            "ContentHash" varchar(64) NOT NULL,
            "Action" varchar(20) NOT NULL,
            "Method" varchar(20) NOT NULL,
            "IpAddress" varchar(64) NULL,
            "UserAgent" varchar(300) NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_LegalAceptaciones_UserId_DocumentKey" ON "LegalAceptaciones" ("UserId", "DocumentKey");""",
        """CREATE INDEX IF NOT EXISTS "IX_LegalAceptaciones_DocumentVersionId" ON "LegalAceptaciones" ("DocumentVersionId");"""
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "LegalVersionesDocumento" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DocumentKey" TEXT NOT NULL,
            "Version" TEXT NOT NULL,
            "Title" TEXT NOT NULL,
            "Content" TEXT NOT NULL,
            "ContentHash" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "RequiresReacceptance" INTEGER NOT NULL DEFAULT 1,
            "ChangeSummary" TEXT NULL,
            "CreatedByUserId" INTEGER NULL,
            "CreatedAt" TEXT NOT NULL,
            "PublishedByUserId" INTEGER NULL,
            "PublishedAt" TEXT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LegalVersionesDocumento_DocumentKey_Version" ON "LegalVersionesDocumento" ("DocumentKey", "Version");""",
        """
        CREATE TABLE IF NOT EXISTS "LegalAceptaciones" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "DocumentKey" TEXT NOT NULL,
            "DocumentVersionId" INTEGER NOT NULL,
            "Version" TEXT NOT NULL,
            "ContentHash" TEXT NOT NULL,
            "Action" TEXT NOT NULL,
            "Method" TEXT NOT NULL,
            "IpAddress" TEXT NULL,
            "UserAgent" TEXT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_LegalAceptaciones_UserId_DocumentKey" ON "LegalAceptaciones" ("UserId", "DocumentKey");""",
        """CREATE INDEX IF NOT EXISTS "IX_LegalAceptaciones_DocumentVersionId" ON "LegalAceptaciones" ("DocumentVersionId");"""
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
                logger?.LogWarning(ex, "LegalSchemaGuard statement failed; legal documents may be unavailable.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("LegalSchemaGuard applied (LegalVersionesDocumento, LegalAceptaciones).");
        }
    }
}
