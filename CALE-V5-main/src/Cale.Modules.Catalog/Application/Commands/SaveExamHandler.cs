using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Catalog.Application.Commands;

public sealed class SaveExamHandler
{
    private readonly ICatalogStore _store;
    private readonly IClock _clock;

    public SaveExamHandler(ICatalogStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<ExamDto> CreateAsync(
        SaveExamRequest request,
        int userId,
        CancellationToken ct)
    {
        var exam = Exam.Create(
            request.Name,
            request.Description,
            request.BankId,
            request.QuestionCount,
            request.TimeMinutes,
            request.AllowedAttempts,
            request.Randomize,
            userId,
            request.StartsAt,
            request.EndsAt,
            _clock.UtcNow);
        await _store.AddExamAsync(exam, ct);
        await _store.SaveChangesAsync(ct);
        return Map(exam);
    }

    public async Task<ExamDto> UpdateAsync(
        int id,
        SaveExamRequest request,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var exam = await Owned(id, userId, isAdmin, ct);
        exam.Update(
            request.Name,
            request.Description,
            request.BankId,
            request.QuestionCount,
            request.TimeMinutes,
            request.AllowedAttempts,
            request.Randomize,
            request.StartsAt,
            request.EndsAt,
            _clock.UtcNow);
        await _store.SaveChangesAsync(ct);
        return Map(exam);
    }

    public async Task PublishAsync(
        int id,
        bool published,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var exam = await Owned(id, userId, isAdmin, ct);
        if (published)
        {
            await EnsureReadyToPublishAsync(exam, ct);
        }

        exam.SetPublished(published, _clock.UtcNow);
        await _store.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        int id,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var exam = await Owned(id, userId, isAdmin, ct);
        exam.SetPublished(false, _clock.UtcNow);
        exam.SetActive(false);
        await _store.SaveChangesAsync(ct);
    }

    private async Task EnsureReadyToPublishAsync(Exam exam, CancellationToken ct)
    {
        if (exam.BankId is null)
        {
            return;
        }

        var pending = await _store.CountQuestionsNeedingReviewAsync(exam.BankId.Value, ct);
        if (pending > 0)
        {
            throw new DomainException(
                $"Hay {pending} pregunta(s) sin clave revisada. Márcalas antes de publicar.",
                400,
                "exam_needs_answer_review");
        }
    }

    private async Task<Exam> Owned(
        int id,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var exam = await _store.GetExamAsync(id, ct)
            ?? throw new NotFoundException("Exam not found.", "exam_not_found");
        if (!exam.CanEdit(userId, isAdmin))
        {
            throw new ForbiddenException("You cannot edit this exam.");
        }

        return exam;
    }

    private static ExamDto Map(Exam exam) => new(
        exam.Id,
        exam.Name,
        exam.Description,
        exam.BankId,
        exam.QuestionCount,
        exam.TimeMinutes,
        exam.AllowedAttempts,
        exam.Randomize,
        exam.Published,
        exam.IsActive,
        exam.CreatedById,
        exam.StartsAt,
        exam.EndsAt);
}
