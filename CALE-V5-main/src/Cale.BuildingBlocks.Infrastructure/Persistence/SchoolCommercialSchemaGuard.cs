using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Creates commercial directory tables. Runs even when FeatureSchema is off.
/// </summary>
public static class SchoolCommercialSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "SchoolCommercialProfiles" (
            "SchoolUserId" integer NOT NULL PRIMARY KEY,
            "PublicationStatus" character varying(32) NOT NULL,
            "About" character varying(1200) NOT NULL DEFAULT '',
            "LicenseCategories" character varying(120) NOT NULL DEFAULT '',
            "CoverMediaId" uuid NULL,
            "LogoMediaId" uuid NULL,
            "Slug" character varying(80) NULL,
            "WhatsApp" character varying(32) NULL,
            "PublicEmail" character varying(320) NULL,
            "CommerciallySuspendedAt" timestamp with time zone NULL,
            "CommercialSuspensionReason" character varying(500) NULL,
            "PublishedAt" timestamp with time zone NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """ALTER TABLE "SchoolCommercialProfiles" ADD COLUMN IF NOT EXISTS "WhatsApp" character varying(32) NULL;""",
        """ALTER TABLE "SchoolCommercialProfiles" ADD COLUMN IF NOT EXISTS "PublicEmail" character varying(320) NULL;""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SchoolCommercialProfiles_Slug" ON "SchoolCommercialProfiles" ("Slug");""",
        """CREATE INDEX IF NOT EXISTS "IX_SchoolCommercialProfiles_PublicationStatus" ON "SchoolCommercialProfiles" ("PublicationStatus");""",
        """
        CREATE TABLE IF NOT EXISTS "SchoolCommercialPosts" (
            "Id" serial PRIMARY KEY,
            "SchoolUserId" integer NOT NULL,
            "Body" character varying(1500) NOT NULL,
            "MediaId" uuid NULL,
            "Status" character varying(32) NOT NULL,
            "PublishedAt" timestamp with time zone NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SchoolCommercialPosts_School_Status" ON "SchoolCommercialPosts" ("SchoolUserId", "Status");""",
        """
        CREATE TABLE IF NOT EXISTS "SchoolCommercialPromotions" (
            "Id" serial PRIMARY KEY,
            "SchoolUserId" integer NOT NULL,
            "Title" character varying(120) NOT NULL,
            "Body" character varying(800) NOT NULL,
            "MediaId" uuid NULL,
            "StartsOn" date NOT NULL,
            "EndsOn" date NOT NULL,
            "Status" character varying(32) NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SchoolCommercialPromotions_School_Status_Dates" ON "SchoolCommercialPromotions" ("SchoolUserId", "Status", "StartsOn", "EndsOn");""",
        """
        CREATE TABLE IF NOT EXISTS "SchoolCommercialEvents" (
            "Id" bigserial PRIMARY KEY,
            "SchoolUserId" integer NOT NULL,
            "EventType" character varying(32) NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SchoolCommercialEvents_School_Type_Created" ON "SchoolCommercialEvents" ("SchoolUserId", "EventType", "CreatedAt");"""
    ];

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
                foreach (var sql in PostgresStatements)
                {
                    await db.Database.ExecuteSqlRawAsync(sql, ct);
                }
            }
            else if (db.Database.IsSqlite())
            {
                await EnsureSqliteAsync(db, ct);
            }

            logger?.LogInformation("SchoolCommercialSchemaGuard applied.");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "SchoolCommercialSchemaGuard failed; the commercial directory stays empty until it succeeds.");
        }
    }

    private static async Task EnsureSqliteAsync(CaleDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "SchoolCommercialProfiles" (
                "SchoolUserId" INTEGER NOT NULL PRIMARY KEY,
                "PublicationStatus" TEXT NOT NULL,
                "About" TEXT NOT NULL DEFAULT '',
                "LicenseCategories" TEXT NOT NULL DEFAULT '',
                "CoverMediaId" TEXT NULL,
                "LogoMediaId" TEXT NULL,
                "Slug" TEXT NULL,
                "WhatsApp" TEXT NULL,
                "PublicEmail" TEXT NULL,
                "CommerciallySuspendedAt" TEXT NULL,
                "CommercialSuspensionReason" TEXT NULL,
                "PublishedAt" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """,
            ct);
        await TryAddSqliteColumnAsync(db,
            """ALTER TABLE "SchoolCommercialProfiles" ADD COLUMN "WhatsApp" TEXT NULL;""", ct);
        await TryAddSqliteColumnAsync(db,
            """ALTER TABLE "SchoolCommercialProfiles" ADD COLUMN "PublicEmail" TEXT NULL;""", ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SchoolCommercialProfiles_Slug" ON "SchoolCommercialProfiles" ("Slug");""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_SchoolCommercialProfiles_PublicationStatus" ON "SchoolCommercialProfiles" ("PublicationStatus");""",
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "SchoolCommercialPosts" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "SchoolUserId" INTEGER NOT NULL,
                "Body" TEXT NOT NULL,
                "MediaId" TEXT NULL,
                "Status" TEXT NOT NULL,
                "PublishedAt" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """,
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_SchoolCommercialPosts_School_Status" ON "SchoolCommercialPosts" ("SchoolUserId", "Status");""",
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "SchoolCommercialPromotions" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "SchoolUserId" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Body" TEXT NOT NULL,
                "MediaId" TEXT NULL,
                "StartsOn" TEXT NOT NULL,
                "EndsOn" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """,
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_SchoolCommercialPromotions_School_Status_Dates" ON "SchoolCommercialPromotions" ("SchoolUserId", "Status", "StartsOn", "EndsOn");""",
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "SchoolCommercialEvents" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "SchoolUserId" INTEGER NOT NULL,
                "EventType" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """,
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_SchoolCommercialEvents_School_Type_Created" ON "SchoolCommercialEvents" ("SchoolUserId", "EventType", "CreatedAt");""",
            ct);
    }

    private static async Task TryAddSqliteColumnAsync(CaleDbContext db, string sql, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch
        {
            // Column already exists.
        }
    }
}
