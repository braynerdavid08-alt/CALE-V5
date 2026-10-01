using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent creation of the user requests table (question proposals and ideas for the admin).
/// Runs even when FeatureSchema is disabled.
/// </summary>
public static class UserRequestSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "SolicitudesUsuario" (
            "Id" serial PRIMARY KEY,
            "UserId" integer NOT NULL,
            "UserName" varchar(200) NOT NULL,
            "UserRole" varchar(30) NOT NULL,
            "Kind" varchar(20) NOT NULL,
            "Status" varchar(20) NOT NULL,
            "Title" varchar(200) NULL,
            "Message" varchar(4000) NULL,
            "PayloadJson" text NULL,
            "AdminNote" varchar(1000) NULL,
            "ReviewedById" integer NULL,
            "ReviewedAt" timestamp with time zone NULL,
            "CreatedQuestionId" integer NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesUsuario_Status_CreatedAt" ON "SolicitudesUsuario" ("Status", "CreatedAt");""",
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesUsuario_UserId" ON "SolicitudesUsuario" ("UserId");"""
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "SolicitudesUsuario" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "UserName" TEXT NOT NULL,
            "UserRole" TEXT NOT NULL,
            "Kind" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "Title" TEXT NULL,
            "Message" TEXT NULL,
            "PayloadJson" TEXT NULL,
            "AdminNote" TEXT NULL,
            "ReviewedById" INTEGER NULL,
            "ReviewedAt" TEXT NULL,
            "CreatedQuestionId" INTEGER NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesUsuario_Status_CreatedAt" ON "SolicitudesUsuario" ("Status", "CreatedAt");""",
        """CREATE INDEX IF NOT EXISTS "IX_SolicitudesUsuario_UserId" ON "SolicitudesUsuario" ("UserId");"""
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
                logger?.LogWarning(ex, "UserRequestSchemaGuard statement failed; user requests may be unavailable.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("UserRequestSchemaGuard applied (SolicitudesUsuario).");
        }
    }
}
