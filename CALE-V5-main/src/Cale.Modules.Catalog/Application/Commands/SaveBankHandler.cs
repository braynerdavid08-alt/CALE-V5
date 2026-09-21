using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Catalog.Application.Commands;

public sealed class SaveBankHandler
{
    private readonly ICatalogStore _store;
    private readonly IClock _clock;

    public SaveBankHandler(ICatalogStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<BankDto> CreateAsync(
        SaveBankRequest request,
        CancellationToken ct)
    {
        var bank = Bank.Create(request.Name, request.Description, _clock.UtcNow);
        bank.SetActive(request.IsActive);
        await _store.AddBankAsync(bank, ct);
        await _store.SaveChangesAsync(ct);
        return new BankDto(bank.Id, bank.Name, bank.Description, bank.IsActive, 0);
    }

    public async Task<BankDto> UpdateAsync(
        int id,
        SaveBankRequest request,
        CancellationToken ct)
    {
        var bank = await _store.GetBankAsync(id, ct)
            ?? throw new NotFoundException("Bank not found.", "bank_not_found");
        bank.Update(request.Name, request.Description);
        bank.SetActive(request.IsActive);
        await _store.SaveChangesAsync(ct);
        var count = await _store.CountQuestionsInBankAsync(id, ct);
        return new BankDto(bank.Id, bank.Name, bank.Description, bank.IsActive, count);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var bank = await _store.GetBankAsync(id, ct)
            ?? throw new NotFoundException("Bank not found.", "bank_not_found");

        var examRefs = await _store.CountExamsForBankAsync(id, ct);
        if (examRefs > 0)
        {
            throw new DomainException(
                "No se puede borrar el banco: hay exámenes vinculados. Elimina o reasigna esos exámenes primero.",
                409,
                "bank_in_use");
        }

        await _store.RemoveBankAsync(bank, ct);
        await _store.SaveChangesAsync(ct);
    }
}
