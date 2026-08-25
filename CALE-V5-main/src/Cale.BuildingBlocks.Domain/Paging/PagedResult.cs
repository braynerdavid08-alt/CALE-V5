namespace Cale.BuildingBlocks.Domain.Paging;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems)
{
    public int TotalPages =>
        PageSize <= 0
            ? 0
            : (int)Math.Ceiling(TotalItems / (double)PageSize);
}
