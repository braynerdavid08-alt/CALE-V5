using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Catalog.Domain;

public sealed class Bank
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool SeedCompleted { get; private set; }
    public bool DistributionApplied { get; private set; }
    /// <summary>Null = banco oficial/global CALE; set = banco del instructor.</summary>
    public int? CreatedById { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Bank()
    {
    }

    public static Bank Create(
        string name,
        string? description,
        DateTime utcNow,
        int? createdById = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Bank name is required.", 400, "invalid_name");
        }

        return new Bank
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            IsActive = true,
            CreatedById = createdById,
            CreatedAt = utcNow
        };
    }

    public bool IsVisibleTo(int userId, bool isAdmin) =>
        isAdmin || CreatedById is null || CreatedById == userId;

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Bank name is required.", 400, "invalid_name");
        }

        Name = name.Trim();
        Description = description?.Trim();
    }

    public void SetActive(bool active) => IsActive = active;

    public void MarkSeedCompleted() => SeedCompleted = true;
}
