using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Infrastructure;

public static class IdentitySeed
{
    public const string AdminEmail = "admin@cale.local";
    public const string AdminPassword = "Admin123!";

    public static async Task EnsureAdminAsync(
        CaleDbContext db,
        IPasswordHasher hasher,
        IClock clock,
        CancellationToken ct = default)
    {
        var exists = await db.Set<User>()
            .AnyAsync(x => x.Email == AdminEmail, ct);
        if (exists)
        {
            return;
        }

        var admin = User.CreateAdmin(
            "Administrador",
            AdminEmail,
            hasher.Hash(AdminPassword),
            clock.UtcNow);

        db.Set<User>().Add(admin);
        await db.SaveChangesAsync(ct);
    }
}
