using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.Commands;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Domain;

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
        var answerByQuestion = answers
            .GroupBy(x => x.QuestionId)
            .ToDictionary(g => g.Key, g => g.First());

        var loaded = await _catalog.ListQuestionsByIdsAsync(
            snapshot.Select(x => x.QuestionId).Distinct().ToList(),
            ct);
        var byId = loaded.ToDictionary(q => q.Id);

        var questions = new List<ReviewQuestionDto>();
        foreach (var item in snapshot.OrderBy(x => x.Order))
        {
            answerByQuestion.TryGetValue(item.QuestionId, out var answer);
            byId.TryGetValue(item.QuestionId, out var live);

            questions.Add(live is not null
                ? FromCatalog(item, live, answer)
                : FromSnapshot(item, answer));
        }

        return new ReviewResponse(FinishExamHandler.Map(attempt), questions);
    }

    private static ReviewQuestionDto FromCatalog(
        AttemptQuestion item,
        Question question,
        AttemptAnswer? answer)
    {
        return new ReviewQuestionDto(
            question.Id,
            item.Order,
            question.Text,
            question.Type,
            question.ImageUrl,
            ClearImportReviewNoise(question.Explanation),
            answer?.IsCorrect ?? false,
            question.Options
                .Select(o => new ReviewOptionDto(
                    o.Id,
                    o.Text,
                    o.IsCorrect,
                    answer?.OptionId == o.Id,
                    o.ImageUrl))
                .ToList());
    }

    private static ReviewQuestionDto FromSnapshot(
        AttemptQuestion item,
        AttemptAnswer? answer)
    {
        var text = string.IsNullOrWhiteSpace(answer?.QuestionTextSnapshot)
            ? $"Pregunta {item.Order} (ya no está en el catálogo)"
            : answer!.QuestionTextSnapshot!;
        var type = string.IsNullOrWhiteSpace(answer?.QuestionTypeSnapshot)
            ? "Seleccion multiple"
            : answer!.QuestionTypeSnapshot!;
        var isCorrect = answer?.IsCorrect ?? false;
        var options = BuildSnapshotOptions(answer);

        return new ReviewQuestionDto(
            item.QuestionId,
            item.Order,
            text,
            type,
            ImageUrl: null,
            Explanation: null,
            isCorrect,
            options);
    }

    private static IReadOnlyList<ReviewOptionDto> BuildSnapshotOptions(AttemptAnswer? answer)
    {
        if (answer is null)
        {
            return
            [
                new ReviewOptionDto(0, "Sin respuesta", false, true, null)
            ];
        }

        var options = new List<ReviewOptionDto>();
        var selected = answer.SelectedOptionSnapshot?.Trim();
        var correct = answer.CorrectOptionSnapshot?.Trim();
        var syntheticId = 1;

        if (!string.IsNullOrWhiteSpace(selected))
        {
            var selectedIsCorrect = string.Equals(
                selected,
                correct,
                StringComparison.OrdinalIgnoreCase);
            options.Add(new ReviewOptionDto(
                syntheticId++,
                selected!,
                selectedIsCorrect,
                Selected: true,
                ImageUrl: null));
        }
        else
        {
            options.Add(new ReviewOptionDto(
                syntheticId++,
                "Sin respuesta",
                false,
                Selected: true,
                ImageUrl: null));
        }

        if (!string.IsNullOrWhiteSpace(correct)
            && !string.Equals(selected, correct, StringComparison.OrdinalIgnoreCase))
        {
            options.Add(new ReviewOptionDto(
                syntheticId,
                correct!,
                IsCorrect: true,
                Selected: false,
                ImageUrl: null));
        }

        return options;
    }

    private static string? ClearImportReviewNoise(string? explanation)
    {
        if (string.IsNullOrWhiteSpace(explanation))
        {
            return null;
        }

        if (explanation.Contains("Importada sin clave", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return explanation.Trim();
    }
}
