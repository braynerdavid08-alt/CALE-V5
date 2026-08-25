using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;

namespace Cale.Modules.Catalog.Application.Queries;

public sealed class ListBlocksHandler
{
    private readonly ICatalogStore _store;

    public ListBlocksHandler(ICatalogStore store) => _store = store;

    public async Task<IReadOnlyList<BlockDto>> HandleAsync(CancellationToken ct)
    {
        var blocks = await _store.ListBlocksAsync(ct);
        return blocks.Select(x => new BlockDto(x.Id, x.Name)).ToList();
    }
}
