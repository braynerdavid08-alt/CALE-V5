using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Presentation.Application;
using Cale.Modules.Presentation.Application.Abstractions;
using Cale.Modules.Presentation.Application.DTOs;
using Cale.Modules.Presentation.Domain;

namespace Cale.Modules.Presentation.Application.Commands;

public sealed class PresentationCommandHandler
{
    private readonly IPresentationStore _store;

    public PresentationCommandHandler(IPresentationStore store) => _store = store;

    public async Task<PresentationDetailDto> CreateAsync(
        CreatePresentationRequest request,
        int userId,
        int? schoolId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var deck = PresentationDeck.Create(
            userId,
            request.Title,
            request.Description,
            request.Category,
            schoolId,
            request.GroupId,
            now);

        await _store.AddAsync(deck, ct);
        await _store.SaveChangesAsync(ct);

        var (slideTitle, bg, elements) = PresentationTemplates.Build(request.TemplateKey);
        var slide = PresentationSlide.Create(deck.Id, 0, slideTitle, null, bg, elements, now);
        await _store.ReplaceSlidesAsync(deck.Id, [slide], ct);
        deck.MarkSlideCount(1, userId, now);
        await _store.SaveChangesAsync(ct);

        var loaded = await RequireOwnedAsync(deck.Id, userId, schoolId, isAdmin: false, ct);
        return MapDetail(loaded);
    }

    public async Task<PresentationDetailDto> ImportFromOutlinesAsync(
        string title,
        string? description,
        string? category,
        IReadOnlyList<ImportedSlideOutline> outlines,
        int userId,
        int? schoolId,
        CancellationToken ct)
    {
        if (outlines.Count == 0)
        {
            throw new DomainException("El archivo no contiene diapositivas.", 400, "empty_import");
        }

        var now = DateTime.UtcNow;
        var deck = PresentationDeck.Create(
            userId,
            title,
            description,
            category,
            schoolId,
            groupId: null,
            now);

        await _store.AddAsync(deck, ct);
        await _store.SaveChangesAsync(ct);

        var slides = new List<PresentationSlide>();
        for (var i = 0; i < outlines.Count; i++)
        {
            var outline = outlines[i];
            string slideTitle;
            string bg;
            string elements;
            if (!string.IsNullOrWhiteSpace(outline.ElementsJson))
            {
                slideTitle = outline.Title;
                bg = outline.BackgroundJson ?? PresentationTemplates.LightBackground;
                elements = outline.ElementsJson;
            }
            else
            {
                (slideTitle, bg, elements) = PresentationExchangeService.BuildSlideFromOutline(outline);
                if (!string.IsNullOrWhiteSpace(outline.BackgroundJson))
                {
                    bg = outline.BackgroundJson;
                }
            }

            slides.Add(PresentationSlide.Create(
                deck.Id,
                i,
                slideTitle,
                outline.Notes,
                bg,
                elements,
                now));
        }

        await _store.ReplaceSlidesAsync(deck.Id, slides, ct);
        deck.MarkSlideCount(slides.Count, userId, now);
        await _store.SaveChangesAsync(ct);

        var loaded = await RequireOwnedAsync(deck.Id, userId, schoolId, isAdmin: false, ct);
        return MapDetail(loaded);
    }

    public async Task<PresentationDetailDto> SaveDocumentAsync(
        int id,
        SavePresentationDocumentRequest request,
        int userId,
        int? schoolUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        var deck = await RequireOwnedAsync(id, userId, schoolUserId, isAdmin, ct);
        var now = DateTime.UtcNow;
        deck.Rename(request.Title, request.Description, request.Category, userId, now);
        deck.Associate(request.GroupId, userId, now);
        if (request.ThumbnailUrl is not null)
        {
            deck.SetThumbnail(request.ThumbnailUrl, userId, now);
        }

        if (request.Slides is null || request.Slides.Count == 0)
        {
            throw new DomainException(
                "La presentación debe tener al menos una diapositiva.",
                400,
                "empty_slides");
        }

        var slides = new List<PresentationSlide>();
        for (var i = 0; i < request.Slides.Count; i++)
        {
            var s = request.Slides[i];
            slides.Add(PresentationSlide.Create(
                deck.Id,
                i,
                s.Title,
                s.Notes,
                s.BackgroundJson,
                s.ElementsJson,
                now));
        }

        await _store.ReplaceSlidesAsync(deck.Id, slides, ct);
        deck.MarkSlideCount(slides.Count, userId, now);
        await _store.SaveChangesAsync(ct);

        var loaded = await RequireOwnedAsync(id, userId, schoolUserId, isAdmin, ct);
        return MapDetail(loaded);
    }

    public async Task UpdateMetaAsync(
        int id,
        UpdatePresentationMetaRequest request,
        int userId,
        int? schoolUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        var deck = await RequireOwnedAsync(id, userId, schoolUserId, isAdmin, ct);
        var now = DateTime.UtcNow;
        deck.Rename(request.Title, request.Description, request.Category, userId, now);
        deck.Associate(request.GroupId, userId, now);
        await _store.SaveChangesAsync(ct);
    }

    public async Task<PresentationDetailDto> DuplicateAsync(
        int id,
        int userId,
        int? schoolUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        var source = await RequireOwnedAsync(id, userId, schoolUserId, isAdmin, ct);
        var now = DateTime.UtcNow;
        var copy = source.Duplicate(userId, now);
        await _store.AddAsync(copy, ct);
        await _store.SaveChangesAsync(ct);

        var slides = source.Slides
            .OrderBy(s => s.Position)
            .Select((s, i) => s.Clone(copy.Id, i, now))
            .ToList();
        await _store.ReplaceSlidesAsync(copy.Id, slides, ct);
        copy.MarkSlideCount(slides.Count, userId, now);
        await _store.SaveChangesAsync(ct);

        var loaded = await RequireOwnedAsync(copy.Id, userId, schoolUserId, isAdmin: false, ct);
        return MapDetail(loaded);
    }

    public async Task DeleteAsync(
        int id,
        int userId,
        int? schoolUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        var deck = await RequireOwnedAsync(id, userId, schoolUserId, isAdmin, ct);
        deck.SoftDelete(userId, DateTime.UtcNow);
        await _store.SoftDeleteAsync(deck, ct);
        await _store.SaveChangesAsync(ct);
    }

    private async Task<PresentationDeck> RequireOwnedAsync(
        int id,
        int userId,
        int? schoolUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        var deck = await _store.GetWithSlidesAsync(id, ct)
            ?? throw new NotFoundException("Presentación no encontrada.");
        if (!deck.IsActive)
        {
            throw new NotFoundException("Presentación no encontrada.");
        }

        if (!deck.CanManage(userId, isAdmin)
            && !await _store.UserCanAccessAsync(id, userId, schoolUserId, ct))
        {
            throw new ForbiddenException("No tienes permiso para esta presentación.");
        }

        return deck;
    }

    internal static PresentationDetailDto MapDetail(PresentationDeck deck) =>
        new(
            deck.Id,
            deck.Title,
            deck.Description,
            deck.Category,
            deck.GroupId,
            deck.SchoolId,
            deck.ThumbnailUrl,
            deck.SlideCount,
            deck.CreatedAt,
            deck.UpdatedAt,
            deck.UpdatedByUserId,
            deck.Slides
                .OrderBy(s => s.Position)
                .Select(s => new PresentationSlideDto(
                    s.Id,
                    s.Position,
                    s.Title,
                    s.Notes,
                    s.BackgroundJson,
                    s.ElementsJson))
                .ToList());
}
