using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;

namespace Cale.Modules.Catalog.Application.Queries;

public sealed class ListQuestionsForReviewHandler
{
    private readonly ICatalogStore _store;

    public ListQuestionsForReviewHandler(ICatalogStore store) => _store = store;

    public async Task<IReadOnlyList<QuestionReviewDto>> HandleAsync(
        int bankId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        _ = await _store.GetBankAsync(bankId, ct)
            ?? throw new NotFoundException("Bank not found.", "bank_not_found");

        var questions = await _store.ListQuestionsForReviewAsync(bankId, ct);
        if (!isAdmin)
        {
            questions = questions.Where(q => q.CreatedById == userId).ToList();
        }

        return questions
            .Select(q => new QuestionReviewDto(
                q.Id,
                q.Text,
                q.Type,
                q.BankId,
                q.BlockId,
                q.Topic,
                q.Explanation,
                ExamImportMarkers.NeedsReview(q.Explanation),
                q.IsActive,
                q.Options
                    .OrderBy(o => o.Id)
                    .Select(o => new OptionDto(o.Id, o.Text, o.IsCorrect, o.ImageUrl))
                    .ToList()))
            .ToList();
    }
}
