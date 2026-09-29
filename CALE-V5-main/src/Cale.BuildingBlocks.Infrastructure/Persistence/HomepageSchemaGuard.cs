using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent creation of the homepage CMS tables. Runs even when FeatureSchema is disabled
/// in Production; without these tables /api/public/home serves the empty emergency landing.
/// </summary>
public static class HomepageSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "HomepageSettings" (
            "Id" integer NOT NULL PRIMARY KEY,
            "HeroBadge" varchar(120) NOT NULL,
            "HeroTitle" varchar(200) NOT NULL,
            "HeroTitleHighlight" varchar(200) NOT NULL,
            "HeroDescription" varchar(2000) NOT NULL,
            "HeroCtaPrimaryLabel" varchar(80) NOT NULL,
            "HeroCtaPrimaryPath" varchar(200) NOT NULL,
            "HeroCtaSecondaryLabel" varchar(80) NOT NULL,
            "HeroVideoUrl" varchar(500) NULL,
            "HeroImageUrl" varchar(500) NULL,
            "HeroImageUrlMobile" varchar(500) NULL,
            "HeroImageAlt" varchar(200) NOT NULL,
            "HeroImageEnabled" boolean NOT NULL,
            "HeroVisible" boolean NOT NULL,
            "BenefitsJson" text NOT NULL,
            "StepsJson" text NOT NULL,
            "StepsSectionTitle" varchar(200) NOT NULL,
            "StepsSectionSubtitle" varchar(500) NOT NULL,
            "SchoolsSectionVisible" boolean NOT NULL,
            "InstructorsSectionVisible" boolean NOT NULL,
            "StatsSectionVisible" boolean NOT NULL,
            "BenefitsSectionVisible" boolean NOT NULL,
            "StepsSectionVisible" boolean NOT NULL,
            "SeoTitle" varchar(200) NOT NULL,
            "SeoDescription" varchar(500) NOT NULL,
            "ContactEmail" varchar(200) NOT NULL,
            "ContactPhone" varchar(80) NOT NULL,
            "AboutHtml" varchar(8000) NOT NULL,
            "BlogIntro" varchar(2000) NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "UpdatedByUserId" integer NULL
        );
        """,
        """ALTER TABLE "HomepageSettings" ADD COLUMN IF NOT EXISTS "HeroImageUrlMobile" varchar(500) NULL;""",
        """ALTER TABLE "HomepageSettings" ADD COLUMN IF NOT EXISTS "HeroVisible" boolean NOT NULL DEFAULT true;""",
        """ALTER TABLE "HomepageSettings" ADD COLUMN IF NOT EXISTS "SchoolsSectionVisible" boolean NOT NULL DEFAULT true;""",
        """ALTER TABLE "HomepageSettings" ADD COLUMN IF NOT EXISTS "InstructorsSectionVisible" boolean NOT NULL DEFAULT true;""",
        """ALTER TABLE "HomepageSettings" ADD COLUMN IF NOT EXISTS "StatsSectionVisible" boolean NOT NULL DEFAULT true;""",
        """ALTER TABLE "HomepageSettings" ADD COLUMN IF NOT EXISTS "BenefitsSectionVisible" boolean NOT NULL DEFAULT true;""",
        """ALTER TABLE "HomepageSettings" ADD COLUMN IF NOT EXISTS "StepsSectionVisible" boolean NOT NULL DEFAULT true;""",
        """
        CREATE TABLE IF NOT EXISTS "HomepageStatSettings" (
            "Id" serial PRIMARY KEY,
            "Key" varchar(40) NOT NULL,
            "Label" varchar(120) NOT NULL,
            "SubLabel" varchar(120) NOT NULL,
            "Icon" varchar(40) NOT NULL,
            "Mode" varchar(20) NOT NULL,
            "ManualValue" varchar(80) NULL,
            "LastComputedValue" varchar(80) NULL,
            "LastComputedDisplay" varchar(80) NULL,
            "Visible" boolean NOT NULL,
            "SortOrder" integer NOT NULL,
            "LastComputedAt" timestamp with time zone NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_HomepageStatSettings_Key" ON "HomepageStatSettings" ("Key");""",
        """
        CREATE TABLE IF NOT EXISTS "HomepageAudits" (
            "Id" bigserial PRIMARY KEY,
            "ActorUserId" integer NOT NULL,
            "Area" varchar(80) NOT NULL,
            "StatKey" varchar(40) NULL,
            "PreviousValue" varchar(200) NULL,
            "NewValue" varchar(200) NULL,
            "Note" varchar(500) NULL,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_HomepageAudits_CreatedAt" ON "HomepageAudits" ("CreatedAt");"""
    ];

    public static async Task EnsureAsync(
        CaleDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || !db.Database.IsNpgsql())
        {
            return;
        }

        var failures = 0;
        foreach (var sql in PostgresStatements)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch (Exception ex)
            {
                failures++;
                logger?.LogWarning(ex, "HomepageSchemaGuard statement failed; public landing may use the fallback.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("HomepageSchemaGuard applied (HomepageSettings/StatSettings/Audits).");
        }
    }
}
