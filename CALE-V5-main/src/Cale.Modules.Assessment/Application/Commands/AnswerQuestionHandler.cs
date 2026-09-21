using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Application.Abstractions;

namespace Cale.Modules.Assessment.Application.Commands;

public sealed class AnswerQuestionHandler
{
    private readonly IAttemptStore _attempts;
    private readonly ICatalogStore _catalog;
    private readonly IClock _clock;

    public AnswerQuestionHandler(
        IAttemptStore attempts,
        ICatalogStore catalog,
        IClock clock)
    {
        _attempts = attempts;
        _catalog = catalog;
        _clock = clock;
    }

    public async Task HandleAsync(
        int attemptId,
        int userId,
        AnswerRequest request,
        CancellationToken ct)
    {
        var attempt = await _attempts.GetAsync(attemptId, ct)
            ?? throw new NotFoundException("Attempt not found.", "attempt_not_found");
        attempt.EnsureOwned(userId);
        if (!attempt.IsOpen(_clock.UtcNow))
        {
            throw new ForbiddenException("Attempt is closed.", "attempt_closed");
        }

        var snapshotRows = await _attempts.ListQuestionsAsync(attemptId, ct);
        var row = snapshotRows.FirstOrDefault(x => x.QuestionId == request.QuestionId)
            ?? throw new DomainException(
                "Question is not part of this attempt.",
                400,
                "question_not_in_attempt");

        string questionText;
        string questionType;
        string? selectedText;
        string? correctText;
        bool isCorrect;

        var parsed = row.ParsedSnapshot();
        if (parsed is not null && parsed.Options.Count > 0)
        {
            var selected = parsed.FindOption(request.OptionId)
                ?? throw new DomainException("Option not found.", 400, "option_not_found");
            var correct = parsed.CorrectOption()
                ?? throw new DomainException(
                    "Question snapshot has no correct option.",
                    400,
                    "invalid_snapshot");
            questionText = parsed.Text;
            questionType = parsed.Type;
            selectedText = selected.Text;
            correctText = correct.Text;
            isCorrect = selected.IsCorrect;
        }
        else
        {
            // Legacy attempts created before immutable snapshots.
            var question = await _catalog.GetQuestionAsync(request.QuestionId, ct)
                ?? throw new NotFoundException("Question not found.", "question_not_found");
            var selected = question.Options.FirstOrDefault(x => x.Id == request.OptionId)
                ?? throw new DomainException("Option not found.", 400, "option_not_found");
            var correct = question.Options.First(x => x.IsCorrect);
            questionText = question.Text;
            questionType = question.Type;
            selectedText = selected.Text;
            correctText = correct.Text;
            isCorrect = selected.IsCorrect;
        }

        var existing = await _attempts.FindAnswerAsync(
            attemptId,
            request.QuestionId,
            ct);
        if (existing is null)
        {
            var answer = AttemptAnswer.Create(
                attemptId,
                request.QuestionId,
                request.OptionId,
                isCorrect,
                questionText,
                selectedText,
                correctText,
                questionType);
            await _attempts.AddAnswerAsync(answer, ct);
        }
        else
        {
            existing.Update(
                request.OptionId,
                isCorrect,
                selectedText,
                correctText);
        }

        await _attempts.SaveChangesAsync(ct);
    }
}
