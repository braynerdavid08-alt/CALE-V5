using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Idempotent creation of the public shop tables (helmets / gear sold via WhatsApp).
/// </summary>
public static class ShopSchemaGuard
{
    private static readonly string[] PostgresStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "ShopSettings" (
            "Id" integer NOT NULL PRIMARY KEY,
            "PageTitle" varchar(120) NOT NULL,
            "PageSubtitle" varchar(500) NOT NULL,
            "HeroImageUrl" varchar(500) NULL,
            "WhatsApp" varchar(20) NULL,
            "CtaLabel" varchar(80) NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ShopProducts" (
            "Id" serial PRIMARY KEY,
            "Name" varchar(120) NOT NULL,
            "Slug" varchar(140) NOT NULL,
            "ShortDescription" varchar(280) NOT NULL,
            "Description" varchar(2000) NULL,
            "ImageUrl" varchar(500) NULL,
            "PriceCop" numeric(18,2) NOT NULL,
            "CompareAtPriceCop" numeric(18,2) NULL,
            "PromoLabel" varchar(40) NULL,
            "Category" varchar(60) NOT NULL,
            "Sizes" varchar(120) NULL,
            "Stock" integer NOT NULL,
            "IsActive" boolean NOT NULL,
            "IsFeatured" boolean NOT NULL,
            "SortOrder" integer NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """,
        """CREATE INDEX IF NOT EXISTS "IX_ShopProducts_IsActive" ON "ShopProducts" ("IsActive");""",
        """CREATE INDEX IF NOT EXISTS "IX_ShopProducts_SortOrder" ON "ShopProducts" ("SortOrder");"""
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
                logger?.LogWarning(ex, "ShopSchemaGuard statement failed; the public shop may be empty until it succeeds.");
            }
        }

        if (failures == 0)
        {
            logger?.LogInformation("ShopSchemaGuard applied (ShopSettings/ShopProducts).");
        }
    }
}
