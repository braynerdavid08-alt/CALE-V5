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
        IReadOnlyCollection<int> schoolCatalogOwnerIds,
        DateTime utcNow,
        int? authorizedOfficialTheoryExamId,
        int? configuredOfficialTheoryExamId,
        CancellationToken ct)
    {
        var exams = await _store.ListPublishedExamsAsync(ct);
        var allLinks = await _store.ListAllExamGroupExamIdsAsync(ct);
        var staffIds = schoolCatalogOwnerIds.Count > 0
            ? schoolCatalogOwnerIds.ToHashSet()
            : [];
        var affiliated = groupIds.Count > 0 || staffIds.Count > 0;

        // Sin escuela ni grupo: solo exámenes de bancos oficiales no asignados a grupos.
        if (!affiliated)
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

                independent.Add(Map(exam, CanMix(exam, configuredOfficialTheoryExamId)));
            }

            return independent;
        }

        var linkedExamIds = groupIds.Count == 0
            ? new HashSet<int>()
            : (await _store.ListExamGroupsForGroupsAsync(groupIds, ct))
                .Select(x => x.ExamId)
                .ToHashSet();

        return exams
            .Where(exam =>
            {
                if (!IsOpen(exam, utcNow))
                {
                    return false;
                }

                // Teórico oficial de la escuela: solo si el estudiante está autorizado.
                if (configuredOfficialTheoryExamId is int configured
                    && configured == exam.Id)
                {
                    return authorizedOfficialTheoryExamId == exam.Id;
                }

                if (authorizedOfficialTheoryExamId is int authorized
                    && authorized == exam.Id)
                {
                    return true;
                }

                // Escuela o grupo: exámenes del personal de la escuela, o asignados al grupo.
                if (staffIds.Contains(exam.CreatedById))
                {
                    return true;
                }

                return linkedExamIds.Contains(exam.Id);
            })
            .Select(exam => Map(exam, CanMix(exam, configuredOfficialTheoryExamId)))
            .ToList();
    }

    private static bool CanMix(Exam exam, int? configuredOfficialTheoryExamId) =>
        exam.AllowedAttempts > 1
        && (configuredOfficialTheoryExamId is null
            || configuredOfficialTheoryExamId != exam.Id);

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
