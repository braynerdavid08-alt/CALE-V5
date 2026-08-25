using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Catalog.Application.Abstractions;

namespace Cale.Modules.Assessment.Application.Commands;

public sealed class FinishExamHandler
{
    private readonly IAttemptStore _attempts;
    private readonly ICatalogStore _catalog;
    private readonly IClock _clock;

    public FinishExamHandler(
        IAttemptStore attempts,
        ICatalogStore catalog,
        IClock clock)
    {
        _attempts = attempts;
        _catalog = catalog;
        _clock = clock;
    }

    public async Task<FinishResponse> HandleAsync(
        int attemptId,
        int userId,
        CancellationToken ct)
    {
        var attempt = await _attempts.GetAsync(attemptId, ct)
            ?? throw new NotFoundException("Attempt not found.", "attempt_not_found");
        attempt.EnsureOwned(userId);

        var snapshot = await _attempts.ListQuestionsAsync(attemptId, ct);
        var answers = await _attempts.ListAnswersAsync(attemptId, ct);
        var correct = 0;
        foreach (var item in snapshot)
        {
            var answer = answers.FirstOrDefault(x => x.QuestionId == item.QuestionId);
            if (answer is not null)
            {
                if (answer.IsCorrect)
                {
                    correct++;
                }

                continue;
            }

            var question = await _catalog.GetQuestionAsync(item.QuestionId, ct);
            var right = question?.Options.FirstOrDefault(x => x.IsCorrect);
            await _attempts.AddAnswerAsync(
                Domain.AttemptAnswer.Create(
                    attemptId,
                    item.QuestionId,
                    null,
                    false,
                    question?.Text,
                    null,
                    right?.Text,
                    question?.Type),
                ct);
        }

        attempt.Finish(correct, _clock.UtcNow);
        await _attempts.SaveChangesAsync(ct);
        return Map(attempt);
    }

    public static FinishResponse Map(Domain.Attempt attempt) => new(
        attempt.Id,
        attempt.TotalQuestions,
        attempt.CorrectCount,
        attempt.Percent,
        attempt.Passed,
        attempt.TimeSeconds);
}
