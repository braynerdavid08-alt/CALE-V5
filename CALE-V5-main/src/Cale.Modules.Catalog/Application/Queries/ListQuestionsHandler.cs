using Cale.BuildingBlocks.Domain.Paging;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;

namespace Cale.Modules.Catalog.Application.Queries;

public sealed class ListQuestionsHandler
{
    private readonly ICatalogStore _store;

    public ListQuestionsHandler(ICatalogStore store) => _store = store;

    /// <summary>Non-admins only see questions from official banks and their own banks.</summary>
    public Task<PagedResult<QuestionListDto>> HandleAsync(
        int page,
        int pageSize,
        int? bankId,
        string? search,
        bool? active,
        int? ownerId,
        int viewerUserId,
        bool viewerIsAdmin,
        CancellationToken ct) =>
        _store.ListQuestionsAsync(
            page is < 1 or > 10_000 ? 1 : page,
            pageSize is < 1 or > 200 ? 20 : pageSize,
            bankId,
            search is { Length: > 200 } ? search[..200] : search,
            active,
            ownerId,
            ct,
            viewerIsAdmin ? null : viewerUserId);
}
