using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Catalog.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.Assessment.Application.Commands;

public sealed class FinishExamHandler
{
    private readonly IAttemptStore _attempts;
    private readonly ICatalogStore _catalog;
    private readonly IAttemptStats _stats;
    private readonly INotificationPublisher _notifications;
    private readonly IClock _clock;
    private readonly ILogger<FinishExamHandler> _logger;

    public FinishExamHandler(
        IAttemptStore attempts,
        ICatalogStore catalog,
        IAttemptStats stats,
        INotificationPublisher notifications,
        IClock clock,
        ILogger<FinishExamHandler> logger)
    {
        _attempts = attempts;
        _catalog = catalog;
        _stats = stats;
        _notifications = notifications;
        _clock = clock;
        _logger = logger;
    }

    public async Task<FinishResponse> HandleAsync(
        int attemptId,
        int userId,
        CancellationToken ct) =>
        await HandleCoreAsync(attemptId, userId, forceExpired: false, ct);

    /// <summary>
    /// Scores and closes an attempt past the grace window so a new Start can proceed.
    /// </summary>
    public async Task<FinishResponse> ReclaimExpiredAsync(
        int attemptId,
        int userId,
        CancellationToken ct) =>
        await HandleCoreAsync(attemptId, userId, forceExpired: true, ct);

    private async Task<FinishResponse> HandleCoreAsync(
        int attemptId,
        int userId,
        bool forceExpired,
        CancellationToken ct)
    {
        try
        {
            var attempt = await _attempts.GetAsync(attemptId, ct)
                ?? throw new NotFoundException("Attempt not found.", "attempt_not_found");
            attempt.EnsureOwned(userId);

            var snapshot = await _attempts.ListQuestionsAsync(attemptId, ct);
            var answers = (await _attempts.ListAnswersAsync(attemptId, ct)).ToList();
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
                var blank = Domain.AttemptAnswer.Create(
                    attemptId,
                    item.QuestionId,
                    null,
                    false,
                    question?.Text,
                    null,
                    right?.Text,
                    question?.Type);

                try
                {
                    await _attempts.AddAnswerAsync(blank, ct);
                    await _attempts.SaveChangesAsync(ct);
                    answers.Add(blank);
                }
                catch (DbUpdateException)
                {
                    // Concurrent finish already inserted this blank answer.
                    _attempts.ClearTrackedChanges();
                    var raced = await _attempts.FindAnswerAsync(
                        attemptId,
                        item.QuestionId,
                        ct);
                    if (raced is not null)
                    {
                        answers.Add(raced);
                        if (raced.IsCorrect)
                        {
                            correct++;
                        }
                    }
                }
            }

            if (forceExpired)
            {
                attempt.CloseExpired(correct, _clock.UtcNow);
            }
            else
            {
                attempt.Finish(correct, _clock.UtcNow);
            }

            var marked = await _attempts.TryMarkFinishedAsync(
                attempt.Id,
                attempt.CorrectCount,
                attempt.Percent,
                attempt.Passed,
                attempt.TimeSeconds,
                attempt.FinishedAt!.Value,
                ct);
            if (!marked)
            {
                throw new ConflictException(
                    "Attempt already finished.",
                    "attempt_finished");
            }

            answers = (await _attempts.ListAnswersAsync(attemptId, ct)).ToList();
            var answerMap = answers
                .GroupBy(x => x.QuestionId)
                .ToDictionary(g => g.Key, g => g.First());
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

            _logger.LogInformation(
                "Exam finished attemptId={AttemptId} userId={UserId} percent={Percent} passed={Passed} correct={Correct}/{Total} forceExpired={ForceExpired}",
                attempt.Id,
                userId,
                attempt.Percent,
                attempt.Passed,
                attempt.CorrectCount,
                attempt.TotalQuestions,
                forceExpired);

            if (!forceExpired)
            {
                var resultLabel = attempt.Passed ? "aprobaste" : "no alcanzaste el mínimo";
                await _notifications.NotifyUsersAsync(
                    [userId],
                    new NotificationDraft(
                        "Resultado de evaluación",
                        $"Completaste el examen: {attempt.Percent}% ({resultLabel}).",
                        NotificationTypes.ExamResult,
                        GroupId: null,
                        RelatedEntity: "attempt",
                        RelatedId: attempt.Id,
                        Link: "/student",
                        Priority: NotificationPriorities.Normal,
                        DedupeKey: $"exam_result:{attempt.Id}"),
                    ct);
            }

            return Map(
                attempt,
                MapBreakdown(byTopic),
                MapBreakdown(byBlock),
                best);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(
                "Exam finish failed attemptId={AttemptId} userId={UserId} code={ErrorCode}",
                attemptId,
                userId,
                ex.ErrorCode);
            throw;
        }
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
