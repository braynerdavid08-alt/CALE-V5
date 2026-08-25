using Cale.BuildingBlocks.Domain.Auth;

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
        return Create(name, email, passwordHash, Roles.Admin, utcNow);
    }

    public static User CreateTeacher(
        string name,
        string email,
        string passwordHash,
        DateTime utcNow,
        int? schoolId = null)
    {
        return Create(name, email, passwordHash, Roles.Teacher, utcNow, schoolId);
    }

    public static User RegisterSchool(
        string name,
        string email,
        string passwordHash,
        DateTime utcNow)
    {
        return Create(name, email, passwordHash, Roles.School, utcNow);
    }

    public void ChangePassword(string newHash) => PasswordHash = newHash;

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

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

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
            CreatedAt = utcNow
        };
    }
}
