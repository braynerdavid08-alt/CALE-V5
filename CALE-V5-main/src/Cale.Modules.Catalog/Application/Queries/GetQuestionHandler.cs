using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;

namespace Cale.Modules.Catalog.Application.Queries;

public sealed class GetQuestionHandler
{
    private readonly ICatalogStore _store;

    public GetQuestionHandler(ICatalogStore store) => _store = store;

    /// <summary>The answer key of a private bank is only shown to its owner (or an admin).</summary>
    public async Task<QuestionDetailDto> HandleAsync(int id, int viewerUserId, bool viewerIsAdmin, CancellationToken ct)
    {
        var question = await _store.GetQuestionAsync(id, ct)
            ?? throw new NotFoundException("Question not found.", "question_not_found");

        if (!viewerIsAdmin && question.CreatedById != viewerUserId)
        {
            var bank = await _store.GetBankAsync(question.BankId, ct);
            if (bank is null || !bank.IsVisibleTo(viewerUserId, isAdmin: false))
            {
                throw new NotFoundException("Question not found.", "question_not_found");
            }
        }

        return new QuestionDetailDto(
            question.Id,
            question.Text,
            question.Type,
            question.BankId,
            question.BlockId,
            question.Topic,
            question.ImageUrl,
            question.Explanation,
            question.IsActive,
            question.CreatedById,
            question.Options.Select(o => new OptionDto(
                o.Id,
                o.Text,
                o.IsCorrect,
                o.ImageUrl)).ToList(),
            question.Subject,
            question.Subtopic);
    }
}
