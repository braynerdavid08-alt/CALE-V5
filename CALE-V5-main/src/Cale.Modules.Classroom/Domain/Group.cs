using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Classroom.Domain;

public sealed class Group
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Code { get; private set; } = "";
    public int? TeacherId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public string? Description { get; private set; }
    public DateTime? StartsOn { get; private set; }

    private Group()
    {
    }

    public static Group Create(
        string name,
        string? description,
        int teacherId,
        DateTime? startsOn,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Group name is required.", 400, "invalid_name");
        }

        return new Group
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            TeacherId = teacherId,
            Code = NewCode(),
            IsActive = true,
            CreatedAt = utcNow,
            StartsOn = startsOn
        };
    }

    public void Update(string name, string? description, DateTime? startsOn)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Group name is required.", 400, "invalid_name");
        }

        Name = name.Trim();
        Description = description?.Trim();
        StartsOn = startsOn;
    }

    public void SetActive(bool active) => IsActive = active;

    public bool CanManage(int userId, bool isAdmin) =>
        isAdmin || TeacherId == userId;

    public static string NewCode() =>
        $"CALE-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
