using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Presentation.Domain;

public sealed class PresentationSlide
{
    public int Id { get; private set; }
    public int PresentationId { get; private set; }
    public int Position { get; private set; }
    public string Title { get; private set; } = "";
    public string? Notes { get; private set; }
    /// <summary>JSON background: { type, color, color2?, imageUrl? }</summary>
    public string BackgroundJson { get; private set; } = "{\"type\":\"solid\",\"color\":\"#ffffff\"}";
    /// <summary>JSON array of slide elements (document model).</summary>
    public string ElementsJson { get; private set; } = "[]";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private PresentationSlide()
    {
    }

    public static PresentationSlide Create(
        int presentationId,
        int position,
        string title,
        string? notes,
        string backgroundJson,
        string elementsJson,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(elementsJson))
        {
            elementsJson = "[]";
        }

        if (string.IsNullOrWhiteSpace(backgroundJson))
        {
            backgroundJson = "{\"type\":\"solid\",\"color\":\"#ffffff\"}";
        }

        EnsureValidElementsJson(elementsJson);

        return new PresentationSlide
        {
            PresentationId = presentationId,
            Position = Math.Max(0, position),
            Title = string.IsNullOrWhiteSpace(title) ? $"Diapositiva {position + 1}" : title.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            BackgroundJson = backgroundJson,
            ElementsJson = elementsJson,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public PresentationSlide Clone(int presentationId, int position, DateTime utcNow) =>
        Create(presentationId, position, Title, Notes, BackgroundJson, ElementsJson, utcNow);

    public static void EnsureValidElementsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        if (json.Length > 2_000_000)
        {
            throw new DomainException(
                "El contenido de la diapositiva es demasiado grande.",
                400,
                "slide_too_large");
        }
    }
}
