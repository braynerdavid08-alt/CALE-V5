using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.Identity.Infrastructure;

public static class IdentitySeed
{
    /// <summary>Legacy demo emails (tests / optional local Seed:DemoUsers only).</summary>
    public const string AdminEmail = "admin@cale.local";
    public const string TeacherEmail = "profesor@cale.local";
    public const string StudentEmail = "estudiante@cale.local";
    public const string SchoolEmail = "escuela@cale.local";

    // Passwords only for optional local demo seed — never used in production bootstrap.
    private const string DemoAdminPassword = "Admin123!";
    private const string DemoTeacherPassword = "Profesor123!";
    private const string DemoStudentPassword = "Estudiante123!";
    private const string DemoSchoolPassword = "Escuela123!";

    private static readonly HashSet<string> DemoEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        AdminEmail,
        TeacherEmail,
        StudentEmail,
        SchoolEmail
    };

    /// <summary>
    /// Creates one temporary admin only when the database has no Admin yet.
    /// Does not reset an existing admin (so they can change email/password safely).
    /// </summary>
    public static async Task EnsureBootstrapAdminIfNoneAsync(
        CaleDbContext db,
        IPasswordHasher hasher,
        IClock clock,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        const string bootstrapEmail = "admin@micale.app";
        const string bootstrapPassword = "CambiarYa123!";
        const string bootstrapName = "Administrador";

        // Drop legacy/personal bootstrap accounts that must not remain in production.
        await RemoveRetiredAccountsAsync(db, ct);

        var hasAdmin = await db.Set<User>().AnyAsync(
            u => u.Role == Roles.Admin || u.Role == "Administrador",
            ct);

        if (hasAdmin)
        {
            logger?.LogInformation("Bootstrap admin skipped: an admin account already exists.");
            return;
        }

        await PurgeAllUsersAsync(db, ct);

        var user = User.CreateAdmin(
            bootstrapName,
            bootstrapEmail,
            hasher.Hash(bootstrapPassword),
            clock.UtcNow);
        user.RequirePasswordChange();
        db.Set<User>().Add(user);
        await db.SaveChangesAsync(ct);

        logger?.LogWarning(
            "Bootstrap admin created ({Email}). Sign in and change email + password immediately.",
            bootstrapEmail);
    }

    private static async Task RemoveRetiredAccountsAsync(CaleDbContext db, CancellationToken ct)
    {
        // Personal credentials previously shared in chat + old demo logins.
        var retired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "braynerdavid08@gmail.com",
            AdminEmail,
            TeacherEmail,
            StudentEmail,
            SchoolEmail
        };

        var users = await db.Set<User>()
            .Where(u => retired.Contains(u.Email))
            .ToListAsync(ct);

        if (users.Count == 0)
        {
            return;
        }

        var ids = users.Select(u => u.Id).ToHashSet();
        var profiles = await db.Set<SchoolProfile>()
            .Where(p => ids.Contains(p.UserId))
            .ToListAsync(ct);
        if (profiles.Count > 0)
        {
            db.Set<SchoolProfile>().RemoveRange(profiles);
        }

        var events = await db.Set<MembershipEvent>()
            .Where(e => ids.Contains(e.SchoolUserId)
                || (e.ActorUserId != null && ids.Contains(e.ActorUserId.Value)))
            .ToListAsync(ct);
        if (events.Count > 0)
        {
            db.Set<MembershipEvent>().RemoveRange(events);
        }

        db.Set<User>().RemoveRange(users);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Ensures a single admin exists from env/config. Prefer <paramref name="password"/> from env,
    /// or <paramref name="passwordHash"/>. When <paramref name="purgeOthers"/> is true, deletes every other user.
    /// </summary>
    public static async Task EnsureSoleAdminAsync(
        CaleDbContext db,
        IPasswordHasher hasher,
        IClock clock,
        string email,
        string name,
        bool purgeOthers,
        string? password = null,
        string? passwordHash = null,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        email = EmailAddress.Normalize(email);
        name = string.IsNullOrWhiteSpace(name) ? "Administrador" : name.Trim();

        string hash;
        if (!string.IsNullOrWhiteSpace(password))
        {
            if (password.Length < 8)
            {
                throw new InvalidOperationException(
                    "Seed:Admin:Password must be at least 8 characters.");
            }

            hash = hasher.Hash(password);
        }
        else if (!string.IsNullOrWhiteSpace(passwordHash))
        {
            hash = passwordHash.Trim();
        }
        else
        {
            throw new InvalidOperationException(
                "Set Seed:Admin:Password (env) or Seed:Admin:PasswordHash.");
        }

        await EnsureUserWithPasswordHashAsync(
            db,
            clock,
            email,
            hash,
            name,
            (n, e, h, now) => User.CreateAdmin(n, e, h, now),
            replaceHash: true,
            ct);

        var admin = await db.Set<User>().FirstAsync(x => x.Email == email, ct);
        if (Roles.Normalize(admin.Role) != Roles.Admin)
        {
            admin.ChangeRole(Roles.Admin);
            await db.SaveChangesAsync(ct);
        }

        if (!admin.EmailConfirmed)
        {
            admin.MarkEmailConfirmed();
            await db.SaveChangesAsync(ct);
        }

        if (purgeOthers)
        {
            var removed = await PurgeAllUsersExceptAsync(db, email, ct);
            logger?.LogInformation(
                "Sole-admin seed: kept {Email}, removed {Count} other account(s).",
                email,
                removed);
        }
        else
        {
            logger?.LogInformation("Sole-admin seed: ensured admin {Email}.", email);
        }
    }

    private static async Task EnsureUserWithPasswordHashAsync(
        CaleDbContext db,
        IClock clock,
        string email,
        string passwordHash,
        string name,
        Func<string, string, string, DateTime, User> factory,
        bool replaceHash,
        CancellationToken ct)
    {
        var user = await db.Set<User>()
            .FirstOrDefaultAsync(x => x.Email == email, ct);

        if (user is null)
        {
            var created = factory(name, email, passwordHash, clock.UtcNow);
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

        if (replaceHash)
        {
            user.ChangePassword(passwordHash);
            changed = true;
        }

        if (!string.Equals(user.Name, name, StringComparison.Ordinal))
        {
            user.UpdateProfile(name, user.Email);
            changed = true;
        }

        if (!user.IsActive)
        {
            user.Activate();
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task EnsureDemoUsersAsync(
        CaleDbContext db,
        IPasswordHasher hasher,
        IClock clock,
        CancellationToken ct = default)
    {
        await PurgeNonDemoUsersAsync(db, ct);

        await EnsureUserAsync(
            db,
            hasher,
            clock,
            AdminEmail,
            DemoAdminPassword,
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
            DemoTeacherPassword,
            "Instructor Demo",
            (name, email, hash, now) => User.CreateTeacher(name, email, hash, now, school.Id),
            ct,
            school.Id);

        await EnsureUserAsync(
            db,
            hasher,
            clock,
            StudentEmail,
            DemoStudentPassword,
            "Estudiante Demo",
            (name, email, hash, now) => User.RegisterStudent(name, email, hash, now, school.Id),
            ct,
            school.Id);
    }

    private static async Task PurgeAllUsersAsync(CaleDbContext db, CancellationToken ct)
    {
        var users = await db.Set<User>().ToListAsync(ct);
        if (users.Count == 0)
        {
            return;
        }

        var ids = users.Select(u => u.Id).ToHashSet();
        var profiles = await db.Set<SchoolProfile>().ToListAsync(ct);
        if (profiles.Count > 0)
        {
            db.Set<SchoolProfile>().RemoveRange(profiles);
        }

        var events = await db.Set<MembershipEvent>()
            .Where(e => ids.Contains(e.SchoolUserId)
                || (e.ActorUserId != null && ids.Contains(e.ActorUserId.Value)))
            .ToListAsync(ct);
        if (events.Count > 0)
        {
            db.Set<MembershipEvent>().RemoveRange(events);
        }

        db.Set<User>().RemoveRange(users);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<int> PurgeAllUsersExceptAsync(
        CaleDbContext db,
        string keepEmail,
        CancellationToken ct)
    {
        var others = await db.Set<User>()
            .Where(u => u.Email != keepEmail)
            .ToListAsync(ct);

        if (others.Count == 0)
        {
            return 0;
        }

        var ids = others.Select(u => u.Id).ToHashSet();

        var profiles = await db.Set<SchoolProfile>()
            .Where(p => ids.Contains(p.UserId))
            .ToListAsync(ct);
        if (profiles.Count > 0)
        {
            db.Set<SchoolProfile>().RemoveRange(profiles);
        }

        var events = await db.Set<MembershipEvent>()
            .Where(e => ids.Contains(e.SchoolUserId) || (e.ActorUserId != null && ids.Contains(e.ActorUserId.Value)))
            .ToListAsync(ct);
        if (events.Count > 0)
        {
            db.Set<MembershipEvent>().RemoveRange(events);
        }

        db.Set<User>().RemoveRange(others);
        await db.SaveChangesAsync(ct);
        return others.Count;
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
                hasher.Hash(DemoSchoolPassword),
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

            if (!hasher.Verify(DemoSchoolPassword, user.PasswordHash))
            {
                user.ChangePassword(hasher.Hash(DemoSchoolPassword));
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

        if (!string.Equals(user.Name, name, StringComparison.Ordinal))
        {
            user.UpdateProfile(name, user.Email);
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
