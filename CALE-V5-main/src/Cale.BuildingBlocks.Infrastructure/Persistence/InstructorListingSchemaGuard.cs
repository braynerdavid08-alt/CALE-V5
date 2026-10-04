using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Creates "InstructorListings" (public directory opt-in). Runs even when FeatureSchema is off.
/// </summary>
public static class InstructorListingSchemaGuard
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
                    CREATE TABLE IF NOT EXISTS "InstructorListings" (
                        "UserId" integer NOT NULL PRIMARY KEY,
                        "CreatedAt" timestamp with time zone NOT NULL
                    );
                    """,
                    ct);
            }
            else if (db.Database.IsSqlite())
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE IF NOT EXISTS "InstructorListings" (
                        "UserId" INTEGER NOT NULL PRIMARY KEY,
                        "CreatedAt" TEXT NOT NULL
                    );
                    """,
                    ct);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "InstructorListingSchemaGuard failed; the public instructor directory stays empty until it succeeds.");
        }
    }
}
