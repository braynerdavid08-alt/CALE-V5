using Cale.BuildingBlocks.Domain.Abstractions;
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
    private readonly IAttemptStats _stats;
    private readonly IClock _clock;

    public FinishExamHandler(
        IAttemptStore attempts,
        ICatalogStore catalog,
        IAttemptStats stats,
        IClock clock)
    {
        _attempts = attempts;
        _catalog = catalog;
        _stats = stats;
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

        answers = await _attempts.ListAnswersAsync(attemptId, ct);
        var answerMap = answers.ToDictionary(x => x.QuestionId);
        var byTopic = new Dictionary<string, (int Correct, int Total)>(
            StringComparer.OrdinalIgnoreCase);
        var byBlock = new Dictionary<string, (int Correct, int Total)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in snapshot)
        {
            var question = await _catalog.GetQuestionAsync(item.QuestionId, ct);
            var topic = string.IsNullOrWhiteSpace(question?.Topic)
                ? "Sin tema"
                : question!.Topic!;
            var block = question is null
                ? "Sin bloque"
                : (await _catalog.GetBlockAsync(question.BlockId, ct))?.Name
                    ?? $"Bloque {question.BlockId}";

            var isCorrect = answerMap.TryGetValue(item.QuestionId, out var ans)
                && ans.IsCorrect;

            Accumulate(byTopic, topic, isCorrect);
            Accumulate(byBlock, block, isCorrect);
        }

        var best = await _stats.BestPercentAsync(userId, ct);
        return Map(
            attempt,
            MapBreakdown(byTopic),
            MapBreakdown(byBlock),
            best);
    }

    public static FinishResponse Map(Domain.Attempt attempt) =>
        Map(attempt, [], [], null);

    public static FinishResponse Map(
        Domain.Attempt attempt,
        IReadOnlyList<ScoreBreakdownDto> byTopic,
        IReadOnlyList<ScoreBreakdownDto> byBlock,
        decimal? bestPercent) => new(
        attempt.Id,
        attempt.TotalQuestions,
        attempt.CorrectCount,
        attempt.Percent,
        attempt.Passed,
        attempt.TimeSeconds,
        byTopic,
        byBlock,
        bestPercent);

    private static void Accumulate(
        IDictionary<string, (int Correct, int Total)> map,
        string key,
        bool isCorrect)
    {
        map.TryGetValue(key, out var current);
        map[key] = (
            current.Correct + (isCorrect ? 1 : 0),
            current.Total + 1);
    }

    private static IReadOnlyList<ScoreBreakdownDto> MapBreakdown(
        IReadOnlyDictionary<string, (int Correct, int Total)> source) =>
        source
            .OrderBy(x => x.Key)
            .Select(x => new ScoreBreakdownDto(
                x.Key,
                x.Value.Correct,
                x.Value.Total,
                x.Value.Total == 0
                    ? 0
                    : Math.Round(
                        (decimal)x.Value.Correct * 100m / x.Value.Total,
                        2)))
            .ToList();
}
