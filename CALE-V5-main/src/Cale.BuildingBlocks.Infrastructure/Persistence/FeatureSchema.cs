using Microsoft.EntityFrameworkCore;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

public static class FeatureSchema
{
    public static async Task EnsureAsync(
        CaleDbContext db,
        CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "SchoolProfiles" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SchoolProfiles" PRIMARY KEY AUTOINCREMENT,
                    "UserId" INTEGER NOT NULL,
                    "LegalName" TEXT NOT NULL,
                    "TaxId" TEXT NOT NULL,
                    "BillingEmail" TEXT NOT NULL,
                    "Phone" TEXT NOT NULL,
                    "Address" TEXT NOT NULL,
                    "City" TEXT NOT NULL,
                    "Department" TEXT NOT NULL,
                    "PlanCode" TEXT NOT NULL,
                    "PlanPriceCop" TEXT NOT NULL,
                    "SubscriptionStatus" TEXT NOT NULL,
                    "MembershipStartsAt" TEXT NULL,
                    "MembershipEndsAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SchoolProfiles_UserId"
                    ON "SchoolProfiles" ("UserId");
                CREATE INDEX IF NOT EXISTS "IX_SchoolProfiles_TaxId"
                    ON "SchoolProfiles" ("TaxId");
                """,
                ct);

            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Usuarios" ADD COLUMN "SchoolId" INTEGER NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "MembershipStartsAt" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "MembershipEndsAt" TEXT NULL;""",
                ct);
            return;
        }

        if (!db.Database.IsSqlServer())
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.Intentos', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Intentos', N'ExpiresAt') IS NULL
                ALTER TABLE dbo.Intentos ADD ExpiresAt datetime2 NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SchoolProfiles (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId int NOT NULL,
                    LegalName nvarchar(250) NOT NULL,
                    TaxId nvarchar(32) NOT NULL,
                    BillingEmail nvarchar(320) NOT NULL,
                    Phone nvarchar(40) NOT NULL,
                    Address nvarchar(300) NOT NULL,
                    City nvarchar(120) NOT NULL,
                    Department nvarchar(120) NOT NULL,
                    PlanCode nvarchar(32) NOT NULL,
                    PlanPriceCop decimal(18,2) NOT NULL,
                    SubscriptionStatus nvarchar(32) NOT NULL,
                    MembershipStartsAt datetime2 NULL,
                    MembershipEndsAt datetime2 NULL,
                    CreatedAt datetime2 NOT NULL
                );
                CREATE UNIQUE INDEX IX_SchoolProfiles_UserId ON dbo.SchoolProfiles(UserId);
                CREATE INDEX IX_SchoolProfiles_TaxId ON dbo.SchoolProfiles(TaxId);
            END

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'MembershipStartsAt') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD MembershipStartsAt datetime2 NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'MembershipEndsAt') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD MembershipEndsAt datetime2 NULL;

            IF COL_LENGTH(N'dbo.Usuarios', N'SchoolId') IS NULL
                ALTER TABLE dbo.Usuarios ADD SchoolId int NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Usuarios_SchoolId' AND object_id = OBJECT_ID(N'dbo.Usuarios'))
                CREATE INDEX IX_Usuarios_SchoolId ON dbo.Usuarios(SchoolId);
            """,
            ct);
    }

    private static async Task TryAddSqliteColumnAsync(
        CaleDbContext db,
        string sql,
        CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch
        {
            // Column already exists on existing databases.
        }
    }
}
