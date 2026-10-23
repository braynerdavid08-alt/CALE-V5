using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Creates "InstructorListings" (public directory opt-in) and marketplace tables.
/// Runs even when FeatureSchema is off.
/// </summary>
public static class InstructorListingSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "InstructorListings" (
            "UserId" integer NOT NULL PRIMARY KEY,
            "CreatedAt" timestamp with time zone NOT NULL,
            "AcceptsPrivateClasses" boolean NOT NULL DEFAULT false
        );
        """,
        """ALTER TABLE "InstructorListings" ADD COLUMN IF NOT EXISTS "AcceptsPrivateClasses" boolean NOT NULL DEFAULT false;""",
        """
        CREATE TABLE IF NOT EXISTS "PrivateClassRequests" (
            "Id" serial PRIMARY KEY,
            "InstructorUserId" integer NOT NULL,
            "StudentUserId" integer NOT NULL,
            "Category" varchar(16) NOT NULL,
            "PreferredSchedule" varchar(200) NOT NULL,
            "Message" varchar(1000) NOT NULL,
            "Status" varchar(20) NOT NULL,
            "Reply" varchar(1000) NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "RespondedAt" timestamp with time zone NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_PrivateClassRequests_StudentUserId_Status" ON "PrivateClassRequests" ("StudentUserId", "Status");""",
        """CREATE INDEX IF NOT EXISTS "IX_PrivateClassRequests_InstructorUserId_Status" ON "PrivateClassRequests" ("InstructorUserId", "Status");""",
        """
        CREATE TABLE IF NOT EXISTS "InstructorRatings" (
            "Id" serial PRIMARY KEY,
            "InstructorUserId" integer NOT NULL,
            "RaterUserId" integer NOT NULL,
            "Stars" integer NOT NULL,
            "Comment" varchar(500) NULL,
            "Hidden" boolean NOT NULL DEFAULT false,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_InstructorRatings_InstructorUserId_RaterUserId" ON "InstructorRatings" ("InstructorUserId", "RaterUserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_InstructorRatings_InstructorUserId" ON "InstructorRatings" ("InstructorUserId");"""
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
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "InstructorListingSchemaGuard failed; the public instructor directory stays empty until it succeeds.");
        }
    }

    private static async Task EnsureSqliteAsync(CaleDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "InstructorListings" (
                "UserId" INTEGER NOT NULL PRIMARY KEY,
                "CreatedAt" TEXT NOT NULL,
                "AcceptsPrivateClasses" INTEGER NOT NULL DEFAULT 0
            );
            """,
            ct);

        if (await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_table_info('InstructorListings') WHERE name = 'AcceptsPrivateClasses';", ct) == 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "InstructorListings" ADD COLUMN "AcceptsPrivateClasses" INTEGER NOT NULL DEFAULT 0;""",
                ct);
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PrivateClassRequests" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "InstructorUserId" INTEGER NOT NULL,
                "StudentUserId" INTEGER NOT NULL,
                "Category" TEXT NOT NULL,
                "PreferredSchedule" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "Reply" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "RespondedAt" TEXT NULL
            );
            """,
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_PrivateClassRequests_StudentUserId_Status" ON "PrivateClassRequests" ("StudentUserId", "Status");""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_PrivateClassRequests_InstructorUserId_Status" ON "PrivateClassRequests" ("InstructorUserId", "Status");""",
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "InstructorRatings" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "InstructorUserId" INTEGER NOT NULL,
                "RaterUserId" INTEGER NOT NULL,
                "Stars" INTEGER NOT NULL,
                "Comment" TEXT NULL,
                "Hidden" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """,
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_InstructorRatings_InstructorUserId_RaterUserId" ON "InstructorRatings" ("InstructorUserId", "RaterUserId");""",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_InstructorRatings_InstructorUserId" ON "InstructorRatings" ("InstructorUserId");""",
            ct);
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
