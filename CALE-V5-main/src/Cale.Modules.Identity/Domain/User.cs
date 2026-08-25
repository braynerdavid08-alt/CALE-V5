using Cale.BuildingBlocks.Domain.Auth;

namespace Cale.Modules.Identity.Domain;

public sealed class User
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public string Role { get; private set; } = Roles.Student;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private User()
    {
    }

    public static User RegisterStudent(
        string name,
        string email,
        string passwordHash,
        DateTime utcNow)
    {
        return Create(name, email, passwordHash, Roles.Student, utcNow);
    }

    public static User CreateAdmin(
        string name,
        string email,
        string passwordHash,
        DateTime utcNow)
    {
        return Create(name, email, passwordHash, Roles.Admin, utcNow);
    }

    public void ChangePassword(string newHash) => PasswordHash = newHash;

    public void Deactivate() => IsActive = false;

    private static User Create(
        string name,
        string email,
        string passwordHash,
        string role,
        DateTime utcNow)
    {
        return new User
        {
            Name = name.Trim(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = utcNow
        };
    }
}
