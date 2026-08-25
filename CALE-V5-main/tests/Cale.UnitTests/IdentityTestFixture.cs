using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.BuildingBlocks.Infrastructure.Security;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cale.UnitTests;

public sealed class IdentityTestFixture : IDisposable
{
    public IdentityTestFixture()
    {
        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Db = new CaleDbContext(
            options,
            new MappingAssemblies(typeof(UserConfiguration).Assembly));
        Db.Database.EnsureCreated();

        Hasher = new PasswordHasher();
        Clock = new FakeClock(
            new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc));
        Tokens = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Key = "CALE-LOCAL-DEV-KEY-CHANGE-ME-32CHARS-MIN",
                Issuer = "Cale.Api",
                Audience = "Cale.Frontend",
                ExpirationHours = 12
            }),
            Clock);
        Users = new UserStore(Db);
    }

    public CaleDbContext Db { get; }
    public UserStore Users { get; }
    public PasswordHasher Hasher { get; }
    public FakeClock Clock { get; }
    public JwtTokenService Tokens { get; }

    public void Dispose() => Db.Dispose();
}
