using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;

namespace Cale.Modules.Catalog.Application.Queries;

public sealed class ListBanksHandler
{
    private readonly ICatalogStore _store;

    public ListBanksHandler(ICatalogStore store) => _store = store;

    public async Task<IReadOnlyList<BankDto>> HandleAsync(
        bool activeOnly,
        CancellationToken ct)
    {
        var banks = await _store.ListBanksAsync(activeOnly, ct);
        var result = new List<BankDto>(banks.Count);
        foreach (var bank in banks)
        {
            var count = await _store.CountQuestionsInBankAsync(bank.Id, ct);
            result.Add(new BankDto(
                bank.Id,
                bank.Name,
                bank.Description,
                bank.IsActive,
                count));
        }

        return result;
    }
}
