using Cale.Modules.Presentation.Domain;

namespace Cale.Modules.Presentation.Application.Abstractions;

public interface IPresentationStore
{
    Task<PresentationDeck?> GetAsync(int id, CancellationToken ct);
    Task<PresentationDeck?> GetWithSlidesAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PresentationDeck>> ListByOwnerAsync(int ownerId, CancellationToken ct);
    Task<IReadOnlyList<PresentationDeck>> ListAccessibleAsync(
        int userId,
        int? schoolUserId,
        CancellationToken ct);
    Task<bool> UserCanAccessAsync(
        int deckId,
        int userId,
        int? schoolUserId,
        CancellationToken ct);
    Task AddAsync(PresentationDeck deck, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task ReplaceSlidesAsync(int presentationId, IReadOnlyList<PresentationSlide> slides, CancellationToken ct);
    Task SoftDeleteAsync(PresentationDeck deck, CancellationToken ct);
}
