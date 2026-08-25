using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Catalog.Application.Commands;

public sealed class SaveQuestionHandler
{
    private readonly ICatalogStore _store;
    private readonly IClock _clock;

    public SaveQuestionHandler(ICatalogStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<int> CreateAsync(
        SaveQuestionRequest request,
        int userId,
        CancellationToken ct)
    {
        await EnsureBankAndBlock(request, ct);
        var options = MapOptions(request.Options);
        var question = Question.Create(
            request.BankId,
            request.BlockId,
            userId,
            request.Text,
            request.Type,
            request.Topic,
            request.ImageUrl,
            request.Explanation,
            options,
            _clock.UtcNow);
        question.SetActive(request.IsActive);
        await _store.AddQuestionAsync(question, ct);
        await _store.SaveChangesAsync(ct);
        return question.Id;
    }

    public async Task UpdateAsync(
        int id,
        SaveQuestionRequest request,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var question = await _store.GetQuestionAsync(id, ct)
            ?? throw new NotFoundException("Question not found.", "question_not_found");
        if (!question.CanEdit(userId, isAdmin))
        {
            throw new ForbiddenException("You cannot edit this question.");
        }

        await EnsureBankAndBlock(request, ct);
        await _store.RemoveOptionsAsync(question, ct);
        question.Replace(
            request.BankId,
            request.BlockId,
            request.Text,
            request.Type,
            request.Topic,
            request.ImageUrl,
            request.Explanation,
            MapOptions(request.Options),
            _clock.UtcNow);
        question.SetActive(request.IsActive);
        await _store.SaveChangesAsync(ct);
    }

    private async Task EnsureBankAndBlock(
        SaveQuestionRequest request,
        CancellationToken ct)
    {
        _ = await _store.GetBankAsync(request.BankId, ct)
            ?? throw new NotFoundException("Bank not found.", "bank_not_found");
        _ = await _store.GetBlockAsync(request.BlockId, ct)
            ?? throw new NotFoundException("Block not found.", "block_not_found");
    }

    private static List<QuestionOption> MapOptions(IReadOnlyList<OptionInput> inputs) =>
        inputs.Select(x => QuestionOption.Create(x.Text, x.IsCorrect, x.ImageUrl))
            .ToList();
}
