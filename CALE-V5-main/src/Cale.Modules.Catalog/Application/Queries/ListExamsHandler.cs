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

    public async Task<IReadOnlyList<ExamDto>> PublishedForStudentAsync(
        IReadOnlyList<int> groupIds,
        DateTime utcNow,
        CancellationToken ct)
    {
        var exams = await _store.ListPublishedExamsAsync(ct);
        var links = await _store.ListExamGroupsForGroupsAsync(
            groupIds.Count == 0 ? [-1] : groupIds,
            ct);
        var linkedExamIds = links.Select(x => x.ExamId).ToHashSet();
        var allLinks = await _store.ListAllExamGroupExamIdsAsync(ct);

        return exams
            .Where(exam =>
            {
                if (exam.StartsAt is not null && utcNow < exam.StartsAt)
                {
                    return false;
                }

                if (exam.EndsAt is not null && utcNow > exam.EndsAt)
                {
                    return false;
                }

                var hasAnyAssignment = allLinks.Contains(exam.Id);
                if (!hasAnyAssignment)
                {
                    return true;
                }

                return linkedExamIds.Contains(exam.Id);
            })
            .Select(Map)
            .ToList();
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
