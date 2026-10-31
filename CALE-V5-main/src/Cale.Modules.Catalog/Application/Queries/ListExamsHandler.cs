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
        return exams.Select(e => Map(e)).ToList();
    }

    public async Task<IReadOnlyList<ExamDto>> PublishedAsync(
        int? ownerId,
        CancellationToken ct)
    {
        var exams = await _store.ListPublishedExamsAsync(ct);
        if (ownerId is int oid)
        {
            exams = exams.Where(e => e.CreatedById == oid).ToList();
        }

        return exams.Select(e => Map(e)).ToList();
    }

    public async Task<IReadOnlyList<ExamDto>> PublishedForStudentAsync(
        IReadOnlyList<int> groupIds,
        DateTime utcNow,
        int? officialTheoryExamId,
        CancellationToken ct)
    {
        var exams = await _store.ListPublishedExamsAsync(ct);
        var allLinks = await _store.ListAllExamGroupExamIdsAsync(ct);

        // Sin escuela/grupo: solo exámenes de bancos oficiales (CreatedById null) no asignados a grupos.
        if (groupIds.Count == 0)
        {
            var independent = new List<ExamDto>();
            foreach (var exam in exams)
            {
                if (!IsOpen(exam, utcNow))
                {
                    continue;
                }

                if (allLinks.Contains(exam.Id))
                {
                    continue;
                }

                if (exam.BankId is not int bankId)
                {
                    continue;
                }

                var bank = await _store.GetBankAsync(bankId, ct);
                if (bank is null || bank.CreatedById is not null || !bank.IsActive)
                {
                    continue;
                }

                independent.Add(Map(exam, canMixInPractice: true));
            }

            return independent;
        }

        var links = await _store.ListExamGroupsForGroupsAsync(groupIds, ct);
        var linkedExamIds = links.Select(x => x.ExamId).ToHashSet();

        return exams
            .Where(exam =>
            {
                if (!IsOpen(exam, utcNow))
                {
                    return false;
                }

                if (officialTheoryExamId is int oid && oid == exam.Id)
                {
                    return true;
                }

                var hasAnyAssignment = allLinks.Contains(exam.Id);
                if (!hasAnyAssignment)
                {
                    // Unassigned published exams are not public to school students.
                    return false;
                }

                return linkedExamIds.Contains(exam.Id);
            })
            // Con grupo: todos los asignados se pueden mezclar en simulacro personalizado.
            .Select(exam => Map(exam, canMixInPractice: true))
            .ToList();
    }

    private static bool IsOpen(Exam exam, DateTime utcNow)
    {
        if (exam.StartsAt is not null && utcNow < exam.StartsAt)
        {
            return false;
        }

        if (exam.EndsAt is not null && utcNow > exam.EndsAt)
        {
            return false;
        }

        return true;
    }

    private static ExamDto Map(Exam exam, bool canMixInPractice = true) => new(
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
        exam.EndsAt,
        canMixInPractice);
}
