using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;

namespace Cale.Modules.Catalog.Application.Queries;

public sealed class GetQuestionHandler
{
    private readonly ICatalogStore _store;

    public GetQuestionHandler(ICatalogStore store) => _store = store;

    public async Task<QuestionDetailDto> HandleAsync(int id, CancellationToken ct)
    {
        var question = await _store.GetQuestionAsync(id, ct)
            ?? throw new NotFoundException("Question not found.", "question_not_found");

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
                o.ImageUrl)).ToList());
    }
}
