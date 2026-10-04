using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cale.UnitTests;

public sealed class RefreshTokenStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cale-refresh-{Guid.NewGuid():N}.db");

    private CaleDbContext NewDb() => new(
        new DbContextOptionsBuilder<CaleDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=False")
            .ReplaceService<IModelCacheKeyFactory, IdentityOnlyModelKey>()
            .Options,
        new MappingAssemblies(typeof(UserConfiguration).Assembly));

    private sealed class IdentityOnlyModelKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => (context.GetType(), nameof(IdentityOnlyModelKey), designTime);
    }

    private async Task<RefreshTokenStore> StoreAsync(CaleDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "AuthRefreshTokens" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "TokenHash" TEXT NOT NULL,
                "ExpiresAt" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "RevokedAt" TEXT NULL
            );
            """);
        return new RefreshTokenStore(db);
    }

    [Fact]
    public async Task Token_rotated_a_moment_ago_still_works_so_parallel_tabs_stay_signed_in()
    {
        await using var db = NewDb();
        var store = await StoreAsync(db);
        var token = await store.IssueAsync(7, DateTime.UtcNow.AddDays(365));

        Assert.Equal(7, await store.ConsumeAsync(token));
        Assert.Equal(7, await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task Token_rotated_long_ago_is_rejected()
    {
        await using var db = NewDb();
        var store = await StoreAsync(db);
        var token = await store.IssueAsync(7, DateTime.UtcNow.AddDays(365));
        Assert.Equal(7, await store.ConsumeAsync(token));

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "AuthRefreshTokens" SET "RevokedAt" = {DateTime.UtcNow.AddMinutes(-5)};""");

        Assert.Null(await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task Logout_and_password_change_revoke_immediately()
    {
        await using var db = NewDb();
        var store = await StoreAsync(db);
        var loggedOut = await store.IssueAsync(7, DateTime.UtcNow.AddDays(365));
        var rotated = await store.IssueAsync(8, DateTime.UtcNow.AddDays(365));
        Assert.Equal(8, await store.ConsumeAsync(rotated));

        await store.RevokeAsync(loggedOut);
        await store.RevokeAllForUserAsync(8);

        Assert.Null(await store.ConsumeAsync(loggedOut));
        Assert.Null(await store.ConsumeAsync(rotated));
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        await using var db = NewDb();
        var store = await StoreAsync(db);
        var token = await store.IssueAsync(7, DateTime.UtcNow.AddMinutes(-1));

        Assert.Null(await store.ConsumeAsync(token));
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
    }
}
