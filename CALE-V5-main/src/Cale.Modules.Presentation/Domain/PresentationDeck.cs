using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Presentation.Domain;

/// <summary>Instructor-owned slide deck (Mi CALE Presentaciones).</summary>
public sealed class PresentationDeck
{
    public int Id { get; private set; }
    public int OwnerId { get; private set; }
    public int? SchoolId { get; private set; }
    public int? GroupId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public string Category { get; private set; } = "Otro";
    public string? ThumbnailUrl { get; private set; }
    public int SlideCount { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int UpdatedByUserId { get; private set; }

    private readonly List<PresentationSlide> _slides = [];
    public IReadOnlyList<PresentationSlide> Slides => _slides;

    private PresentationDeck()
    {
    }

    public static PresentationDeck Create(
        int ownerId,
        string title,
        string? description,
        string? category,
        int? schoolId,
        int? groupId,
        DateTime utcNow)
    {
        if (ownerId <= 0)
        {
            throw new DomainException("Owner is required.", 400, "invalid_owner");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("El título es obligatorio.", 400, "invalid_title");
        }

        return new PresentationDeck
        {
            OwnerId = ownerId,
            SchoolId = schoolId,
            GroupId = groupId,
            Title = PresentationFieldLimits.Title(title, "Presentación"),
            Description = PresentationFieldLimits.Description(description),
            Category = NormalizeCategory(category),
            ThumbnailUrl = null,
            SlideCount = 0,
            IsActive = true,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            UpdatedByUserId = ownerId
        };
    }

    public void Rename(string title, string? description, string? category, int actorId, DateTime utcNow)
    {
        EnsureActive();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("El título es obligatorio.", 400, "invalid_title");
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (!string.IsNullOrWhiteSpace(category))
        {
            Category = NormalizeCategory(category);
        }

        Touch(actorId, utcNow);
    }

    public void Associate(int? groupId, int actorId, DateTime utcNow)
    {
        EnsureActive();
        GroupId = groupId;
        Touch(actorId, utcNow);
    }

    public void SetThumbnail(string? url, int actorId, DateTime utcNow)
    {
        ThumbnailUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        Touch(actorId, utcNow);
    }

    public void MarkSlideCount(int count, int actorId, DateTime utcNow)
    {
        SlideCount = Math.Max(0, count);
        Touch(actorId, utcNow);
    }

    public void SoftDelete(int actorId, DateTime utcNow)
    {
        EnsureActive();
        IsActive = false;
        Touch(actorId, utcNow);
    }

    public void AttachLoadedSlides(IEnumerable<PresentationSlide> slides)
    {
        _slides.Clear();
        _slides.AddRange(slides.OrderBy(s => s.Position));
        SlideCount = _slides.Count;
    }

    public bool CanManage(int userId, bool isAdmin) =>
        isAdmin || OwnerId == userId;

    public PresentationDeck Duplicate(int newOwnerId, DateTime utcNow) =>
        Create(
            newOwnerId,
            $"{Title} (copia)",
            Description,
            Category,
            SchoolId,
            null,
            utcNow);

    private void Touch(int actorId, DateTime utcNow)
    {
        UpdatedAt = utcNow;
        UpdatedByUserId = actorId;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException("La presentación no está disponible.", 404, "presentation_inactive");
        }
    }

    private static string NormalizeCategory(string? category)
    {
        var value = string.IsNullOrWhiteSpace(category) ? "Otro" : category.Trim();
        return value.Length > 80 ? value[..80] : value;
    }
}
