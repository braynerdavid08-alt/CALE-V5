using Microsoft.EntityFrameworkCore;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

public static class FeatureSchema
{
    public static async Task EnsureAsync(
        CaleDbContext db,
        CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.Intentos', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Intentos', N'ExpiresAt') IS NULL
                ALTER TABLE dbo.Intentos ADD ExpiresAt datetime2 NULL;
            """,
            ct);
    }
}
