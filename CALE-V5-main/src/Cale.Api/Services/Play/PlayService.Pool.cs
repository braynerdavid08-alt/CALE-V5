using Cale.BuildingBlocks.Domain.Classroom;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Classroom.Domain;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cale.Api.Services.Play;

public sealed partial class PlayService
{
    public const int DailySchoolShare = 3;
    public const int SignsQuestionCount = 40;
    private static readonly TimeSpan PoolTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromMinutes(5);

    // ───────────────────────── School question pool ─────────────────────────

    /// <summary>
    /// Questions from published exams that the student's instructors or school created,
    /// or that were assigned to one of the student's groups.
    /// </summary>
    internal async Task<IReadOnlyList<int>> SchoolQuestionIdsAsync(int userId, CancellationToken ct)
    {
        var key = $"play:pool:{userId}";
        if (_cache.TryGetValue(key, out IReadOnlyList<int>? cached) && cached is not null)
        {
            return cached;
        }

        var ids = await LoadSchoolQuestionIdsAsync(userId, ct);
        CacheSmall(key, ids, PoolTtl);
        return ids;
    }

    private async Task<IReadOnlyList<int>> LoadSchoolQuestionIdsAsync(int userId, CancellationToken ct)
    {
        var schoolId = await _db.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.SchoolId)
            .FirstOrDefaultAsync(ct);
        var groupIds = await ActiveGroupIdsAsync(userId, ct);

        var staffIds = await _db.Set<Group>().AsNoTracking()
            .Where(g => groupIds.Contains(g.Id) && g.TeacherId != null)
            .Select(g => g.TeacherId!.Value)
            .ToListAsync(ct);
        if (schoolId is int sid)
        {
            staffIds.Add(sid);
            staffIds.AddRange(await _db.Set<User>().AsNoTracking()
                .Where(u => u.SchoolId == sid && u.IsActive && u.Id != userId)
                .Select(u => u.Id)
                .ToListAsync(ct));
        }

        staffIds = staffIds.Distinct().ToList();
        if (groupIds.Count == 0 && staffIds.Count == 0)
        {
            return [];
        }

        var now = _clock.UtcNow;
        var linkedExamIds = await _db.Set<ExamGroupLink>().AsNoTracking()
            .Where(l => groupIds.Contains(l.GroupId) && (l.StartsAt == null || l.StartsAt <= now))
            .Select(l => l.ExamId)
            .ToListAsync(ct);

        var exams = await _db.Set<Exam>().AsNoTracking()
            .Where(e => e.Published
                && e.IsActive
                && (e.StartsAt == null || e.StartsAt <= now)
                && (linkedExamIds.Contains(e.Id) || staffIds.Contains(e.CreatedById)))
            .Select(e => new { e.Id, e.BankId })
            .ToListAsync(ct);
        if (exams.Count == 0)
        {
            return [];
        }

        var examIds = exams.Select(e => e.Id).ToList();
        var picked = await _db.Set<ExamQuestion>().AsNoTracking()
            .Where(x => examIds.Contains(x.ExamId))
            .Select(x => new { x.ExamId, x.QuestionId })
            .ToListAsync(ct);
        var examsWithList = picked.Select(x => x.ExamId).ToHashSet();
        var questionIds = picked.Select(x => x.QuestionId).Distinct().ToList();
        var bankIds = exams
            .Where(e => e.BankId is not null && !examsWithList.Contains(e.Id))
            .Select(e => e.BankId!.Value)
            .Distinct()
            .ToList();
        var officialBanks = await OfficialBankIdsAsync(ct);

        return await _db.Set<Question>().AsNoTracking()
            .Where(q => q.IsActive
                && !officialBanks.Contains(q.BankId)
                && (questionIds.Contains(q.Id) || bankIds.Contains(q.BankId))
                && (q.Explanation == null || !q.Explanation.Contains("Importada sin clave"))
                && q.Options.Count() >= 2
                && q.Options.Count(o => o.IsCorrect) == 1)
            .Select(q => q.Id)
            .ToListAsync(ct);
    }

    private async Task<List<int>> ActiveGroupIdsAsync(int userId, CancellationToken ct) =>
        await _db.Set<GroupMember>().AsNoTracking()
            .Where(m => m.UserId == userId && (m.Status == MemberStatuses.Active || m.Status == "Active"))
            .Select(m => m.GroupId)
            .ToListAsync(ct);

    // ───────────────────────── Señal relámpago: school questions ─────────────────────────

    public async Task<IReadOnlyList<PlayQuestionDto>> GetSignsQuestionsAsync(int userId, CancellationToken ct)
    {
        var pool = await SchoolQuestionIdsAsync(userId, ct);
        if (pool.Count == 0)
        {
            return [];
        }

        var seed = Random.Shared.Next();
        var picked = pool
            .OrderBy(id => StableHash(seed, id))
            .Take(SignsQuestionCount)
            .ToList();
        var questions = await LoadQuestionsAsync(picked, ct);
        return picked
            .Where(questions.ContainsKey)
            .Select(id => ToDto(questions[id], seed))
            .ToList();
    }

    public async Task<QuickCheckResultDto> CheckSignsQuestionAsync(
        int userId,
        PlayAnswerRequest request,
        CancellationToken ct)
    {
        var pool = await SchoolQuestionIdsAsync(userId, ct);
        if (!pool.Contains(request.QuestionId))
        {
            throw new DomainException("Question is not available for this game.", 400, "question_invalid");
        }

        var question = await LoadQuestionAsync(request.QuestionId, ct);
        var correctId = CorrectOptionId(question);
        var correct = correctId == request.OptionId;
        if (!correct)
        {
            await UpsertMistakeAsync(userId, question.Id, _clock.UtcNow, ct);
            await _db.SaveChangesAsync(ct);
        }

        return new QuickCheckResultDto(correct, correctId);
    }

    // ───────────────────────── Cached catalog lookups ─────────────────────────

    private async Task<List<int>> OfficialBankIdsAsync(CancellationToken ct)
    {
        const string key = "play:official-banks";
        if (_cache.TryGetValue(key, out List<int>? cached) && cached is not null)
        {
            return cached;
        }

        var ids = await _db.Set<Bank>().AsNoTracking()
            .Where(b => b.IsActive && b.CreatedById == null)
            .Select(b => b.Id)
            .ToListAsync(ct);
        CacheSmall(key, ids, CatalogTtl);
        return ids;
    }

    internal async Task<List<int>> OfficialQuestionIdsAsync(CancellationToken ct)
    {
        const string key = "play:official-questions";
        if (_cache.TryGetValue(key, out List<int>? cached) && cached is not null)
        {
            return cached;
        }

        var bankIds = await OfficialBankIdsAsync(ct);
        var ids = await _db.Set<Question>().AsNoTracking()
            .Where(q => q.IsActive && bankIds.Contains(q.BankId))
            .Select(q => q.Id)
            .ToListAsync(ct);
        CacheSmall(key, ids, CatalogTtl);
        return ids;
    }

    private async Task<List<int>> OfficialBlockIdsAsync(CancellationToken ct)
    {
        const string key = "play:official-blocks";
        if (_cache.TryGetValue(key, out List<int>? cached) && cached is not null)
        {
            return cached;
        }

        var bankIds = await OfficialBankIdsAsync(ct);
        var ids = await _db.Set<Question>().AsNoTracking()
            .Where(q => q.IsActive && bankIds.Contains(q.BankId))
            .Select(q => q.BlockId)
            .Distinct()
            .ToListAsync(ct);
        CacheSmall(key, ids, CatalogTtl);
        return ids;
    }

    // The shared cache has a SizeLimit, so every entry must declare a Size.
    private void CacheSmall<T>(string key, T value, TimeSpan ttl) =>
        _cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Size = 1 });
}
