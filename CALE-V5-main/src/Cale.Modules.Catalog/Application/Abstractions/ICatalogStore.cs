using Cale.BuildingBlocks.Domain.Paging;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Catalog.Application.Abstractions;

public interface ICatalogStore
{
    Task<IReadOnlyList<Bank>> ListBanksAsync(bool activeOnly, CancellationToken ct);
    Task<Bank?> GetBankAsync(int id, CancellationToken ct);
    Task AddBankAsync(Bank bank, CancellationToken ct);
    Task<int> CountQuestionsInBankAsync(int bankId, CancellationToken ct);

    Task<IReadOnlyList<Block>> ListBlocksAsync(CancellationToken ct);
    Task<Block?> GetBlockAsync(int id, CancellationToken ct);
    Task<Block?> GetBlockByNameAsync(string name, CancellationToken ct);
    Task AddBlockAsync(Block block, CancellationToken ct);
    Task<Bank?> GetBankByNameAsync(string name, CancellationToken ct);

    Task<PagedResult<QuestionListDto>> ListQuestionsAsync(
        int page,
        int pageSize,
        int? bankId,
        string? search,
        bool? active,
        int? ownerId,
        CancellationToken ct);

    Task<Question?> GetQuestionAsync(int id, CancellationToken ct);
    Task AddQuestionAsync(Question question, CancellationToken ct);
    Task RemoveOptionsAsync(Question question, CancellationToken ct);

    Task<IReadOnlyList<Exam>> ListExamsAsync(int? ownerId, CancellationToken ct);
    Task<Exam?> GetExamAsync(int id, CancellationToken ct);
    Task AddExamAsync(Exam exam, CancellationToken ct);
    Task<IReadOnlyList<Exam>> ListPublishedExamsAsync(CancellationToken ct);

    Task<ExamGroupLink?> FindExamGroupAsync(
        int examId,
        int groupId,
        CancellationToken ct);

    Task AddExamGroupAsync(ExamGroupLink link, CancellationToken ct);
    Task<IReadOnlyList<ExamGroupLink>> ListExamGroupsAsync(
        int examId,
        CancellationToken ct);

    Task<IReadOnlyList<ExamGroupLink>> ListExamGroupsForGroupsAsync(
        IReadOnlyList<int> groupIds,
        CancellationToken ct);

    Task<IReadOnlySet<int>> ListAllExamGroupExamIdsAsync(CancellationToken ct);

    Task<IReadOnlyList<Question>> ListActiveQuestionsInBankAsync(
        int bankId,
        CancellationToken ct);

    Task<IReadOnlyList<int>> ListExamQuestionIdsAsync(
        int examId,
        CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
