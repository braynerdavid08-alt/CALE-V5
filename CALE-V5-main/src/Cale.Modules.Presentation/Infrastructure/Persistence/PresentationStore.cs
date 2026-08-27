using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Presentation.Application.Abstractions;
using Cale.Modules.Presentation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Presentation.Infrastructure.Persistence;

public sealed class PresentationStore : IPresentationStore
{
    private readonly CaleDbContext _db;

    public PresentationStore(CaleDbContext db) => _db = db;

    public Task<PresentationDeck?> GetAsync(int id, CancellationToken ct) =>
        _db.Set<PresentationDeck>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PresentationDeck?> GetWithSlidesAsync(int id, CancellationToken ct)
    {
        var deck = await _db.Set<PresentationDeck>()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (deck is null)
        {
            return null;
        }

        var slides = await _db.Set<PresentationSlide>()
            .Where(s => s.PresentationId == id)
            .OrderBy(s => s.Position)
            .AsNoTracking()
            .ToListAsync(ct);
        deck.AttachLoadedSlides(slides);
        return deck;
    }

    public async Task<IReadOnlyList<PresentationDeck>> ListByOwnerAsync(
        int ownerId,
        CancellationToken ct)
    {
        return await _db.Set<PresentationDeck>()
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.IsActive)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(PresentationDeck deck, CancellationToken ct) =>
        await _db.Set<PresentationDeck>().AddAsync(deck, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);

    public async Task ReplaceSlidesAsync(
        int presentationId,
        IReadOnlyList<PresentationSlide> slides,
        CancellationToken ct)
    {
        var existing = await _db.Set<PresentationSlide>()
            .Where(s => s.PresentationId == presentationId)
            .ToListAsync(ct);
        _db.Set<PresentationSlide>().RemoveRange(existing);

        foreach (var slide in slides)
        {
            await _db.Set<PresentationSlide>().AddAsync(slide, ct);
        }
    }

    public Task SoftDeleteAsync(PresentationDeck deck, CancellationToken ct) =>
        Task.CompletedTask;
}
