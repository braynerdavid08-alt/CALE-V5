using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Classroom.Domain;

public sealed class Material
{
    public int Id { get; private set; }
    public int GroupId { get; private set; }
    public int AuthorId { get; private set; }
    public string Module { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public string Type { get; private set; } = "link";
    public string? Url { get; private set; }
    public string? TextContent { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private Material()
    {
    }

    public static Material Publish(
        int groupId,
        int authorId,
        string module,
        string title,
        string? description,
        string type,
        string? url,
        string? textContent,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Material title is required.", 400, "invalid_title");
        }

        return new Material
        {
            GroupId = groupId,
            AuthorId = authorId,
            Module = string.IsNullOrWhiteSpace(module) ? "General" : module.Trim(),
            Title = title.Trim(),
            Description = description?.Trim(),
            Type = string.IsNullOrWhiteSpace(type) ? "link" : type.Trim(),
            Url = url?.Trim(),
            TextContent = textContent?.Trim(),
            IsActive = true,
            CreatedAt = utcNow
        };
    }
}
