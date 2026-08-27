using Cale.BuildingBlocks.Domain.Auth;
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

    private static readonly HashSet<string> DemoEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        AdminEmail,
        TeacherEmail,
        StudentEmail,
        SchoolEmail
    };

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
        // Keep only one account per role (the demo set).
        await PurgeNonDemoUsersAsync(db, ct);

        await EnsureUserAsync(
            db,
            hasher,
            clock,
            AdminEmail,
            AdminPassword,
            "Administrador CALE",
            (name, email, hash, now) => User.CreateAdmin(name, email, hash, now),
            ct);

        await EnsureDemoSchoolAsync(db, hasher, clock, ct);

        var school = await db.Set<User>()
            .AsNoTracking()
            .FirstAsync(x => x.Email == SchoolEmail, ct);

        await EnsureUserAsync(
            db,
            hasher,
            clock,
            TeacherEmail,
            TeacherPassword,
            "Instructor Demo",
            (name, email, hash, now) => User.CreateTeacher(name, email, hash, now, school.Id),
            ct,
            school.Id);

        await EnsureUserAsync(
            db,
            hasher,
            clock,
            StudentEmail,
            StudentPassword,
            "Estudiante Demo",
            (name, email, hash, now) => User.RegisterStudent(name, email, hash, now, school.Id),
            ct,
            school.Id);
    }

    private static async Task PurgeNonDemoUsersAsync(CaleDbContext db, CancellationToken ct)
    {
        var all = await db.Set<User>().ToListAsync(ct);
        var extras = all.Where(u => !DemoEmails.Contains(u.Email)).ToList();

        if (extras.Count == 0)
        {
            return;
        }

        var extraIds = extras.Select(u => u.Id).ToHashSet();
        var orphanProfiles = await db.Set<SchoolProfile>()
            .Where(p => extraIds.Contains(p.UserId))
            .ToListAsync(ct);

        if (orphanProfiles.Count > 0)
        {
            db.Set<SchoolProfile>().RemoveRange(orphanProfiles);
        }

        db.Set<User>().RemoveRange(extras);
        await db.SaveChangesAsync(ct);
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
            user.MarkEmailConfirmed();
            db.Set<User>().Add(user);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            if (!user.EmailConfirmed)
            {
                user.MarkEmailConfirmed();
            }

            if (!hasher.Verify(SchoolPassword, user.PasswordHash))
            {
                user.ChangePassword(hasher.Hash(SchoolPassword));
            }

            if (!user.IsActive)
            {
                user.Activate();
            }

            await db.SaveChangesAsync(ct);
        }

        var profile = await db.Set<SchoolProfile>()
            .FirstOrDefaultAsync(x => x.UserId == user.Id, ct);
        var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;
        if (profile is null)
        {
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
            return;
        }

        if (!profile.IsCommerciallyActive(clock.UtcNow))
        {
            profile.ActivateOrRenew(plan, clock.UtcNow);
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
        CancellationToken ct,
        int? schoolId = null)
    {
        var user = await db.Set<User>()
            .FirstOrDefaultAsync(x => x.Email == email, ct);

        if (user is null)
        {
            var created = factory(name, email, hasher.Hash(password), clock.UtcNow);
            created.MarkEmailConfirmed();
            db.Set<User>().Add(created);
            await db.SaveChangesAsync(ct);
            return;
        }

        var changed = false;
        if (!user.EmailConfirmed)
        {
            user.MarkEmailConfirmed();
            changed = true;
        }
        if (!hasher.Verify(password, user.PasswordHash))
        {
            user.ChangePassword(hasher.Hash(password));
            changed = true;
        }

        if (!user.IsActive)
        {
            user.Activate();
            changed = true;
        }

        if (schoolId is { } sid && user.SchoolId != sid
            && (Roles.Normalize(user.Role) == Roles.Teacher
                || Roles.Normalize(user.Role) == Roles.Student))
        {
            user.AssignSchool(sid);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
