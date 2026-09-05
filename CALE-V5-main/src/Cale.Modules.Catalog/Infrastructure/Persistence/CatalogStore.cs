using Cale.BuildingBlocks.Domain.Paging;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class CatalogStore : ICatalogStore
{
    private readonly CaleDbContext _db;

    public CatalogStore(CaleDbContext db) => _db = db;

    public async Task<IReadOnlyList<Bank>> ListBanksAsync(
        bool activeOnly,
        CancellationToken ct)
    {
        var query = _db.Set<Bank>().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.OrderBy(x => x.Name).ToListAsync(ct);
    }

    public Task<Bank?> GetBankAsync(int id, CancellationToken ct) =>
        _db.Set<Bank>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddBankAsync(Bank bank, CancellationToken ct) =>
        await _db.Set<Bank>().AddAsync(bank, ct);

    public Task<int> CountQuestionsInBankAsync(int bankId, CancellationToken ct) =>
        _db.Set<Question>().CountAsync(x => x.BankId == bankId && x.IsActive, ct);

    public async Task<IReadOnlyList<QuestionThemeRow>> ListActiveThemeRowsAsync(
        CancellationToken ct) =>
        await _db.Set<Question>()
            .Where(x => x.IsActive)
            .Select(x => new QuestionThemeRow(x.BankId, x.Topic, x.Subject, x.Subtopic))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<QuestionDifficultyRow>> ListActiveDifficultyRowsAsync(
        CancellationToken ct) =>
        await _db.Set<Question>()
            .Where(x => x.IsActive)
            .Select(x => new QuestionDifficultyRow(x.BankId, x.Difficulty))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Block>> ListBlocksAsync(CancellationToken ct) =>
        await _db.Set<Block>().OrderBy(x => x.Name).ToListAsync(ct);

    public Task<Block?> GetBlockAsync(int id, CancellationToken ct) =>
        _db.Set<Block>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Block?> GetBlockByNameAsync(string name, CancellationToken ct) =>
        _db.Set<Block>().FirstOrDefaultAsync(x => x.Name == name, ct);

    public async Task AddBlockAsync(Block block, CancellationToken ct) =>
        await _db.Set<Block>().AddAsync(block, ct);

    public Task<Bank?> GetBankByNameAsync(string name, CancellationToken ct) =>
        _db.Set<Bank>().FirstOrDefaultAsync(x => x.Name == name, ct);

    public async Task<PagedResult<QuestionListDto>> ListQuestionsAsync(
        int page,
        int pageSize,
        int? bankId,
        string? search,
        bool? active,
        int? ownerId,
        CancellationToken ct)
    {
        var query = from q in _db.Set<Question>()
                    join b in _db.Set<Bank>() on q.BankId equals b.Id
                    select new { q, BankName = b.Name };

        if (bankId is not null)
        {
            query = query.Where(x => x.q.BankId == bankId);
        }

        if (active is not null)
        {
            query = query.Where(x => x.q.IsActive == active);
        }

        if (ownerId is not null)
        {
            query = query.Where(x => x.q.CreatedById == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.q.Text.Contains(term) || (x.q.Topic != null && x.q.Topic.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.q.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new QuestionListDto(
                x.q.Id,
                x.q.Text,
                x.q.Type,
                x.q.BankId,
                x.BankName,
                x.q.Topic,
                x.q.IsActive,
                x.q.CreatedById))
            .ToListAsync(ct);

        return new PagedResult<QuestionListDto>(items, page, pageSize, total);
    }

    public Task<Question?> GetQuestionAsync(int id, CancellationToken ct) =>
        _db.Set<Question>()
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddQuestionAsync(Question question, CancellationToken ct) =>
        await _db.Set<Question>().AddAsync(question, ct);

    public Task RemoveOptionsAsync(Question question, CancellationToken ct)
    {
        _db.Set<QuestionOption>().RemoveRange(question.Options);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Exam>> ListExamsAsync(
        int? ownerId,
        CancellationToken ct)
    {
        var query = _db.Set<Exam>().AsQueryable();
        if (ownerId is not null)
        {
            query = query.Where(x => x.CreatedById == ownerId);
        }

        return await query.OrderBy(x => x.Name).ToListAsync(ct);
    }

    public Task<Exam?> GetExamAsync(int id, CancellationToken ct) =>
        _db.Set<Exam>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddExamAsync(Exam exam, CancellationToken ct) =>
        await _db.Set<Exam>().AddAsync(exam, ct);

    public async Task<IReadOnlyList<Exam>> ListPublishedExamsAsync(
        CancellationToken ct) =>
        await _db.Set<Exam>()
            .Where(x => x.Published && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public Task<ExamGroupLink?> FindExamGroupAsync(
        int examId,
        int groupId,
        CancellationToken ct) =>
        _db.Set<ExamGroupLink>().FirstOrDefaultAsync(
            x => x.ExamId == examId && x.GroupId == groupId,
            ct);

    public async Task AddExamGroupAsync(ExamGroupLink link, CancellationToken ct) =>
        await _db.Set<ExamGroupLink>().AddAsync(link, ct);

    public async Task<IReadOnlyList<ExamGroupLink>> ListExamGroupsAsync(
        int examId,
        CancellationToken ct) =>
        await _db.Set<ExamGroupLink>()
            .Where(x => x.ExamId == examId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ExamGroupLink>> ListExamGroupsForGroupsAsync(
        IReadOnlyList<int> groupIds,
        CancellationToken ct) =>
        await _db.Set<ExamGroupLink>()
            .Where(x => groupIds.Contains(x.GroupId))
            .ToListAsync(ct);

    public async Task<IReadOnlySet<int>> ListAllExamGroupExamIdsAsync(
        CancellationToken ct)
    {
        var ids = await _db.Set<ExamGroupLink>()
            .Select(x => x.ExamId)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    public async Task<IReadOnlyList<Question>> ListActiveQuestionsInBankAsync(
        int bankId,
        CancellationToken ct) =>
        await _db.Set<Question>()
            .Include(x => x.Options)
            .Where(x => x.BankId == bankId && x.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Question>> ListQuestionsForReviewAsync(
        int bankId,
        CancellationToken ct) =>
        await _db.Set<Question>()
            .Include(x => x.Options)
            .Where(x =>
                x.BankId == bankId
                && x.IsActive
                && x.Explanation != null
                && x.Explanation.Contains("Importada sin clave"))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> ListExamQuestionIdsAsync(
        int examId,
        CancellationToken ct) =>
        await _db.Set<ExamQuestion>()
            .Where(x => x.ExamId == examId)
            .OrderBy(x => x.Order)
            .Select(x => x.QuestionId)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
