using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Catalog.Application.Queries;

public sealed class ListExamsHandler
{
    private readonly ICatalogStore _store;

    public ListExamsHandler(ICatalogStore store) => _store = store;

    public async Task<IReadOnlyList<ExamDto>> HandleAsync(
        int? ownerId,
        CancellationToken ct)
    {
        var exams = await _store.ListExamsAsync(ownerId, ct);
        return exams.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ExamDto>> PublishedAsync(CancellationToken ct)
    {
        var exams = await _store.ListPublishedExamsAsync(ct);
        return exams.Select(Map).ToList();
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
