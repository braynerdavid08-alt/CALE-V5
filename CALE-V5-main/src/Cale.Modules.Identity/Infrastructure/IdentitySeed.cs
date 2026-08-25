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
    public const string TeacherEmail = "profesor@cale.local";
    public const string TeacherPassword = "Profesor123!";
    public const string StudentEmail = "estudiante@cale.local";
    public const string StudentPassword = "Estudiante123!";
    public const string SchoolEmail = "escuela@cale.local";
    public const string SchoolPassword = "Escuela123!";

    public static async Task EnsureAdminAsync(
        CaleDbContext db,
        IPasswordHasher hasher,
        IClock clock,
        CancellationToken ct = default) =>
        await EnsureDemoUsersAsync(db, hasher, clock, ct);

    public static async Task EnsureDemoUsersAsync(
        CaleDbContext db,
        IPasswordHasher hasher,
        IClock clock,
        CancellationToken ct = default)
    {
        await EnsureUserAsync(
            db,
            hasher,
            clock,
            AdminEmail,
            AdminPassword,
            "Equipo CALE",
            (name, email, hash, now) => User.CreateAdmin(name, email, hash, now),
            ct);

        await EnsureUserAsync(
            db,
            hasher,
            clock,
            TeacherEmail,
            TeacherPassword,
            "Profesor Demo",
            (name, email, hash, now) => User.CreateTeacher(name, email, hash, now),
            ct);

        await EnsureUserAsync(
            db,
            hasher,
            clock,
            StudentEmail,
            StudentPassword,
            "Estudiante Demo",
            (name, email, hash, now) => User.RegisterStudent(name, email, hash, now),
            ct);

        await EnsureDemoSchoolAsync(db, hasher, clock, ct);
    }

    private static async Task EnsureDemoSchoolAsync(
        CaleDbContext db,
        IPasswordHasher hasher,
        IClock clock,
        CancellationToken ct)
    {
        var user = await db.Set<User>()
            .FirstOrDefaultAsync(x => x.Email == SchoolEmail, ct);

        if (user is null)
        {
            user = User.RegisterSchool(
                "Escuela Demo",
                SchoolEmail,
                hasher.Hash(SchoolPassword),
                clock.UtcNow);
            db.Set<User>().Add(user);
            await db.SaveChangesAsync(ct);
        }
        else if (!hasher.Verify(SchoolPassword, user.PasswordHash))
        {
            user.ChangePassword(hasher.Hash(SchoolPassword));
            await db.SaveChangesAsync(ct);
        }

        var profile = await db.Set<SchoolProfile>()
            .FirstOrDefaultAsync(x => x.UserId == user.Id, ct);
        if (profile is null)
        {
            var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;
            profile = SchoolProfile.Create(
                user.Id,
                "Escuela Demo CALE S.A.S.",
                "900000000-1",
                SchoolEmail,
                "3000000000",
                "Calle 1 # 2-3",
                "Bogotá",
                "Cundinamarca",
                plan,
                clock.UtcNow);
            profile.ActivateOrRenew(plan, clock.UtcNow);
            db.Set<SchoolProfile>().Add(profile);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureUserAsync(
        CaleDbContext db,
        IPasswordHasher hasher,
        IClock clock,
        string email,
        string password,
        string name,
        Func<string, string, string, DateTime, User> factory,
        CancellationToken ct)
    {
        var user = await db.Set<User>()
            .FirstOrDefaultAsync(x => x.Email == email, ct);

        if (user is null)
        {
            db.Set<User>().Add(factory(name, email, hasher.Hash(password), clock.UtcNow));
            await db.SaveChangesAsync(ct);
            return;
        }

        if (!hasher.Verify(password, user.PasswordHash))
        {
            user.ChangePassword(hasher.Hash(password));
            await db.SaveChangesAsync(ct);
        }
    }
}
