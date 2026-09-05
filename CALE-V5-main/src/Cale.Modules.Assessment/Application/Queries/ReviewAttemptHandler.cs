using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.Commands;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Catalog.Application.Abstractions;

namespace Cale.Modules.Assessment.Application.Queries;

public sealed class ReviewAttemptHandler
{
    private readonly IAttemptStore _attempts;
    private readonly ICatalogStore _catalog;

    public ReviewAttemptHandler(IAttemptStore attempts, ICatalogStore catalog)
    {
        _attempts = attempts;
        _catalog = catalog;
    }

    public async Task<ReviewResponse> HandleAsync(
        int attemptId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var attempt = await _attempts.GetAsync(attemptId, ct)
            ?? throw new NotFoundException("Attempt not found.", "attempt_not_found");
        if (!isAdmin)
        {
            attempt.EnsureOwned(userId);
        }

        if (attempt.FinishedAt is null)
        {
            throw new ForbiddenException(
                "Review is available after finishing.",
                "attempt_not_finished");
        }

        var snapshot = await _attempts.ListQuestionsAsync(attemptId, ct);
        var answers = await _attempts.ListAnswersAsync(attemptId, ct);
        var loaded = await _catalog.ListQuestionsByIdsAsync(
            snapshot.Select(x => x.QuestionId).ToList(),
            ct);
        var byId = loaded.ToDictionary(q => q.Id);
        var questions = new List<ReviewQuestionDto>();
        foreach (var item in snapshot.OrderBy(x => x.Order))
        {
            if (!byId.TryGetValue(item.QuestionId, out var question))
            {
                throw new NotFoundException(
                    "Question not found.",
                    "question_not_found");
            }

            var answer = answers.FirstOrDefault(x => x.QuestionId == item.QuestionId);
            questions.Add(new ReviewQuestionDto(
                question.Id,
                item.Order,
                question.Text,
                question.Type,
                question.ImageUrl,
                question.Explanation,
                answer?.IsCorrect ?? false,
                question.Options.Select(o => new ReviewOptionDto(
                    o.Id,
                    o.Text,
                    o.IsCorrect,
                    answer?.OptionId == o.Id,
                    o.ImageUrl)).ToList()));
        }

        return new ReviewResponse(FinishExamHandler.Map(attempt), questions);
    }
}
