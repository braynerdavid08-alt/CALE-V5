using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Presentation.Application.Abstractions;
using Cale.Modules.Presentation.Application.Commands;
using Cale.Modules.Presentation.Application.DTOs;

namespace Cale.Modules.Presentation.Application.Queries;

public sealed class PresentationQueryHandler
{
    private readonly IPresentationStore _store;

    public PresentationQueryHandler(IPresentationStore store) => _store = store;

    public async Task<IReadOnlyList<PresentationListItemDto>> ListMineAsync(
        int userId,
        CancellationToken ct)
    {
        var items = await _store.ListByOwnerAsync(userId, ct);
        return items
            .OrderByDescending(x => x.UpdatedAt)
            .Select(MapList)
            .ToList();
    }

    public async Task<PresentationSummaryDto> SummaryAsync(int userId, CancellationToken ct)
    {
        var items = await _store.ListByOwnerAsync(userId, ct);
        var ordered = items.OrderByDescending(x => x.UpdatedAt).ToList();
        return new PresentationSummaryDto(
            ordered.Count,
            ordered.Count == 0 ? null : MapList(ordered[0]));
    }

    public async Task<PresentationDetailDto> GetAsync(
        int id,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var deck = await _store.GetWithSlidesAsync(id, ct)
            ?? throw new NotFoundException("Presentación no encontrada.");
        if (!deck.IsActive)
        {
            throw new NotFoundException("Presentación no encontrada.");
        }

        if (!deck.CanManage(userId, isAdmin))
        {
            throw new ForbiddenException("No tienes permiso para esta presentación.");
        }

        return PresentationCommandHandler.MapDetail(deck);
    }

    private static PresentationListItemDto MapList(Domain.PresentationDeck deck) =>
        new(
            deck.Id,
            deck.Title,
            deck.Description,
            deck.Category,
            deck.GroupId,
            deck.ThumbnailUrl,
            deck.SlideCount,
            deck.CreatedAt,
            deck.UpdatedAt);
}
