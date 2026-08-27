using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

public sealed class User
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public string Role { get; private set; } = Roles.Student;
    public int? SchoolId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public bool MustChangePassword { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public string? EmailConfirmationCodeHash { get; private set; }
    public DateTime? EmailConfirmationExpiresAt { get; private set; }

    private User()
    {
    }

    public static User RegisterStudent(
        string name,
        string email,
        string passwordHash,
        DateTime utcNow,
        int? schoolId = null)
    {
        return Create(name, email, passwordHash, Roles.Student, utcNow, schoolId);
    }

    public static User CreateAdmin(
        string name,
        string email,
        string passwordHash,
        DateTime utcNow)
    {
        var user = Create(name, email, passwordHash, Roles.Admin, utcNow);
        user.MarkEmailConfirmed();
        return user;
    }

    public static User CreateTeacher(
        string name,
        string email,
        string passwordHash,
        DateTime utcNow,
        int? schoolId = null,
        bool emailConfirmed = false)
    {
        var user = Create(name, email, passwordHash, Roles.Teacher, utcNow, schoolId);
        if (emailConfirmed)
        {
            user.MarkEmailConfirmed();
        }

        return user;
    }

    public static User RegisterSchool(
        string name,
        string email,
        string passwordHash,
        DateTime utcNow)
    {
        return Create(name, email, passwordHash, Roles.School, utcNow);
    }

    public void ChangePassword(string newHash)
    {
        PasswordHash = newHash;
        MustChangePassword = false;
    }

    public void RequirePasswordChange() => MustChangePassword = true;

    public void UpdateProfile(string name, string email)
    {
        Name = name.Trim();
        Email = email;
    }

    public void ChangeRole(string role) => Role = role;

    public void AssignSchool(int schoolId)
    {
        if (schoolId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schoolId));
        }

        SchoolId = schoolId;
    }

    public void LeaveSchool() => SchoolId = null;

    public void RecordLogin(DateTime utcNow) => LastLoginAt = utcNow;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void BeginEmailConfirmation(string codeHash, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("Code hash is required.", nameof(codeHash));
        }

        EmailConfirmed = false;
        EmailConfirmationCodeHash = codeHash;
        EmailConfirmationExpiresAt = expiresAtUtc;
    }

    public void MarkEmailConfirmed()
    {
        EmailConfirmed = true;
        EmailConfirmationCodeHash = null;
        EmailConfirmationExpiresAt = null;
    }

    public void ConfirmEmailWithCode(string codeHash, DateTime utcNow)
    {
        if (EmailConfirmed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EmailConfirmationCodeHash)
            || EmailConfirmationExpiresAt is null
            || utcNow > EmailConfirmationExpiresAt.Value)
        {
            throw new DomainException(
                "Confirmation code expired or missing.",
                400,
                "confirmation_expired");
        }

        if (!FixedEquals(EmailConfirmationCodeHash, codeHash))
        {
            throw new DomainException(
                "Invalid confirmation code.",
                400,
                "invalid_confirmation_code");
        }

        MarkEmailConfirmed();
    }

    private static bool FixedEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }

    private static User Create(
        string name,
        string email,
        string passwordHash,
        string role,
        DateTime utcNow,
        int? schoolId = null)
    {
        return new User
        {
            Name = name.Trim(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            SchoolId = schoolId,
            IsActive = true,
            EmailConfirmed = false,
            CreatedAt = utcNow
        };
    }
}
