using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent commercial columns + CommercialPlans table for temporary B2B subscriptions.
/// </summary>
public static class CommercialSchemaGuard
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
                    ALTER TABLE "SchoolProfiles" ADD COLUMN IF NOT EXISTS "PaymentAdminStatus" character varying(32) NOT NULL DEFAULT 'None';
                    ALTER TABLE "SchoolProfiles" ADD COLUMN IF NOT EXISTS "AgreedPriceCop" numeric(18,2) NULL;
                    ALTER TABLE "SchoolProfiles" ADD COLUMN IF NOT EXISTS "CourtesyUntil" timestamp with time zone NULL;
                    ALTER TABLE "SchoolProfiles" ADD COLUMN IF NOT EXISTS "CourtesyReason" character varying(500) NULL;

                    CREATE TABLE IF NOT EXISTS "CommercialPlans" (
                        "Id" serial PRIMARY KEY,
                        "Code" character varying(32) NOT NULL,
                        "LabelEs" character varying(80) NOT NULL,
                        "PriceCop" numeric(18,2) NOT NULL,
                        "DurationMonths" integer NOT NULL,
                        "MaxTeachers" integer NULL,
                        "MaxStudents" integer NULL,
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "IsLegacy" boolean NOT NULL DEFAULT FALSE,
                        "UpdatedAt" timestamp with time zone NOT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_CommercialPlans_Code" ON "CommercialPlans" ("Code");

                    CREATE TABLE IF NOT EXISTS "AssistantUsageDaily" (
                        "Id" serial PRIMARY KEY,
                        "UserId" integer NOT NULL,
                        "SchoolUserId" integer NULL,
                        "Day" date NOT NULL,
                        "MessagesUsed" integer NOT NULL DEFAULT 0,
                        "RequestsUsed" integer NOT NULL DEFAULT 0,
                        "UpdatedAt" timestamp with time zone NOT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_AssistantUsageDaily_User_Day"
                        ON "AssistantUsageDaily" ("UserId", "Day");
                    CREATE INDEX IF NOT EXISTS "IX_AssistantUsageDaily_School_Day"
                        ON "AssistantUsageDaily" ("SchoolUserId", "Day");

                    CREATE TABLE IF NOT EXISTS "AssistantUsageEvents" (
                        "Id" bigserial PRIMARY KEY,
                        "UserId" integer NOT NULL,
                        "SchoolUserId" integer NULL,
                        "Day" date NOT NULL,
                        "Status" character varying(32) NOT NULL,
                        "Kind" character varying(32) NOT NULL,
                        "Note" character varying(500) NULL,
                        "CreatedAt" timestamp with time zone NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_AssistantUsageEvents_School_Day"
                        ON "AssistantUsageEvents" ("SchoolUserId", "Day");
                    CREATE INDEX IF NOT EXISTS "IX_AssistantUsageEvents_User_Day"
                        ON "AssistantUsageEvents" ("UserId", "Day");
                    """,
                    ct);

                await SeedPlansNpgsqlAsync(db, ct);
            }
            else if (db.Database.IsSqlite())
            {
                await EnsureSqliteColumnAsync(db, "SchoolProfiles", "PaymentAdminStatus",
                    """ALTER TABLE "SchoolProfiles" ADD COLUMN "PaymentAdminStatus" TEXT NOT NULL DEFAULT 'None';""", ct);
                await EnsureSqliteColumnAsync(db, "SchoolProfiles", "AgreedPriceCop",
                    """ALTER TABLE "SchoolProfiles" ADD COLUMN "AgreedPriceCop" TEXT NULL;""", ct);
                await EnsureSqliteColumnAsync(db, "SchoolProfiles", "CourtesyUntil",
                    """ALTER TABLE "SchoolProfiles" ADD COLUMN "CourtesyUntil" TEXT NULL;""", ct);
                await EnsureSqliteColumnAsync(db, "SchoolProfiles", "CourtesyReason",
                    """ALTER TABLE "SchoolProfiles" ADD COLUMN "CourtesyReason" TEXT NULL;""", ct);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE IF NOT EXISTS "CommercialPlans" (
                        "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                        "Code" TEXT NOT NULL,
                        "LabelEs" TEXT NOT NULL,
                        "PriceCop" TEXT NOT NULL,
                        "DurationMonths" INTEGER NOT NULL,
                        "MaxTeachers" INTEGER NULL,
                        "MaxStudents" INTEGER NULL,
                        "IsActive" INTEGER NOT NULL DEFAULT 1,
                        "IsLegacy" INTEGER NOT NULL DEFAULT 0,
                        "UpdatedAt" TEXT NOT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_CommercialPlans_Code" ON "CommercialPlans" ("Code");

                    CREATE TABLE IF NOT EXISTS "AssistantUsageDaily" (
                        "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                        "UserId" INTEGER NOT NULL,
                        "SchoolUserId" INTEGER NULL,
                        "Day" TEXT NOT NULL,
                        "MessagesUsed" INTEGER NOT NULL DEFAULT 0,
                        "RequestsUsed" INTEGER NOT NULL DEFAULT 0,
                        "UpdatedAt" TEXT NOT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_AssistantUsageDaily_User_Day"
                        ON "AssistantUsageDaily" ("UserId", "Day");

                    CREATE TABLE IF NOT EXISTS "AssistantUsageEvents" (
                        "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                        "UserId" INTEGER NOT NULL,
                        "SchoolUserId" INTEGER NULL,
                        "Day" TEXT NOT NULL,
                        "Status" TEXT NOT NULL,
                        "Kind" TEXT NOT NULL,
                        "Note" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL
                    );
                    """,
                    ct);

                await SeedPlansSqliteAsync(db, ct);
            }

            logger?.LogInformation("CommercialSchemaGuard applied (plans, courtesy, assistant usage).");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "CommercialSchemaGuard failed; commercial features may be unavailable.");
        }
    }

    private static async Task SeedPlansNpgsqlAsync(CaleDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "CommercialPlans"
                ("Code", "LabelEs", "PriceCop", "DurationMonths", "MaxTeachers", "MaxStudents", "IsActive", "IsLegacy", "UpdatedAt")
            SELECT 'Monthly', 'Mensual', 500000, 1, NULL, NULL, TRUE, FALSE, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM "CommercialPlans" WHERE "Code" = 'Monthly');

            INSERT INTO "CommercialPlans"
                ("Code", "LabelEs", "PriceCop", "DurationMonths", "MaxTeachers", "MaxStudents", "IsActive", "IsLegacy", "UpdatedAt")
            SELECT 'Annual', 'Anual', 5000000, 12, NULL, NULL, TRUE, FALSE, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM "CommercialPlans" WHERE "Code" = 'Annual');

            INSERT INTO "CommercialPlans"
                ("Code", "LabelEs", "PriceCop", "DurationMonths", "MaxTeachers", "MaxStudents", "IsActive", "IsLegacy", "UpdatedAt")
            SELECT 'Semestral', 'Semestral', 800000, 6, NULL, NULL, FALSE, TRUE, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM "CommercialPlans" WHERE "Code" = 'Semestral');
            """,
            ct);
    }

    private static async Task SeedPlansSqliteAsync(CaleDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "CommercialPlans"
                ("Code", "LabelEs", "PriceCop", "DurationMonths", "MaxTeachers", "MaxStudents", "IsActive", "IsLegacy", "UpdatedAt")
            SELECT 'Monthly', 'Mensual', '500000', 1, NULL, NULL, 1, 0, datetime('now')
            WHERE NOT EXISTS (SELECT 1 FROM "CommercialPlans" WHERE "Code" = 'Monthly');

            INSERT INTO "CommercialPlans"
                ("Code", "LabelEs", "PriceCop", "DurationMonths", "MaxTeachers", "MaxStudents", "IsActive", "IsLegacy", "UpdatedAt")
            SELECT 'Annual', 'Anual', '5000000', 12, NULL, NULL, 1, 0, datetime('now')
            WHERE NOT EXISTS (SELECT 1 FROM "CommercialPlans" WHERE "Code" = 'Annual');

            INSERT INTO "CommercialPlans"
                ("Code", "LabelEs", "PriceCop", "DurationMonths", "MaxTeachers", "MaxStudents", "IsActive", "IsLegacy", "UpdatedAt")
            SELECT 'Semestral', 'Semestral', '800000', 6, NULL, NULL, 0, 1, datetime('now')
            WHERE NOT EXISTS (SELECT 1 FROM "CommercialPlans" WHERE "Code" = 'Semestral');
            """,
            ct);
    }

    private static async Task EnsureSqliteColumnAsync(
        CaleDbContext db,
        string table,
        string column,
        string alterSql,
        CancellationToken ct)
    {
        if (await ScalarAsync(db, $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}';", ct) == 0)
        {
            return;
        }

        var exists = await ScalarAsync(
            db,
            $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';",
            ct);
        if (exists == 0)
        {
            await db.Database.ExecuteSqlRawAsync(alterSql, ct);
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
