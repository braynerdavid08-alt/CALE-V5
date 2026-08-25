using Cale.BuildingBlocks.Domain.Paging;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;

namespace Cale.Modules.Catalog.Application.Queries;

public sealed class ListQuestionsHandler
{
    private readonly ICatalogStore _store;

    public ListQuestionsHandler(ICatalogStore store) => _store = store;

    public Task<PagedResult<QuestionListDto>> HandleAsync(
        int page,
        int pageSize,
        int? bankId,
        string? search,
        bool? active,
        int? ownerId,
        CancellationToken ct) =>
        _store.ListQuestionsAsync(
            page < 1 ? 1 : page,
            pageSize is < 1 or > 100 ? 20 : pageSize,
            bankId,
            search,
            active,
            ownerId,
            ct);
}
