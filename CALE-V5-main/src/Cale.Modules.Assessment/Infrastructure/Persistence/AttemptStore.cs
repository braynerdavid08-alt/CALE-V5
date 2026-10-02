using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Application.Abstractions;
using Cale.Modules.Assessment.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Assessment.Infrastructure.Persistence;

public sealed class AttemptStore : IAttemptStore
{
    private readonly CaleDbContext _db;

    public AttemptStore(CaleDbContext db) => _db = db;

    public Task<Attempt?> GetAsync(int id, CancellationToken ct) =>
        _db.Set<Attempt>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(Attempt attempt, CancellationToken ct) =>
        await _db.Set<Attempt>().AddAsync(attempt, ct);

    public async Task AddQuestionsAsync(
        int attemptId,
        IReadOnlyList<AttemptQuestion> questions,
        CancellationToken ct)
    {
        await _db.Set<AttemptQuestion>().AddRangeAsync(questions, ct);
    }

    public async Task AddAttemptWithQuestionsAsync(
        Attempt attempt,
        IReadOnlyList<AttemptQuestion> questions,
        CancellationToken ct)
    {
        try
        {
            await AddAttemptWithQuestionsCoreAsync(attempt, questions, ct);
        }
        catch (DbUpdateException ex) when (LooksLikeMissingAttemptColumn(ex))
        {
            // Self-heal once if Production skipped FeatureSchema / migrations.
            await AttemptSchemaGuard.EnsureAsync(_db, logger: null, ct);
            _db.ChangeTracker.Clear();
            attempt.ClearGeneratedIdForRetry();
            await AddAttemptWithQuestionsCoreAsync(attempt, questions, ct);
        }
    }

    private async Task AddAttemptWithQuestionsCoreAsync(
        Attempt attempt,
        IReadOnlyList<AttemptQuestion> questions,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _db.Set<Attempt>().AddAsync(attempt, ct);
            await _db.SaveChangesAsync(ct);

            foreach (var question in questions)
            {
                // Re-bind attempt id after identity generation.
                var bound = AttemptQuestion.Create(
                    attempt.Id,
                    question.QuestionId,
                    question.Order,
                    question.SnapshotJson);
                await _db.Set<AttemptQuestion>().AddAsync(bound, ct);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    private static bool LooksLikeMissingAttemptColumn(DbUpdateException ex)
    {
        for (var current = (Exception?)ex; current is not null; current = current.InnerException)
        {
            var msg = current.Message;
            if (msg.Contains("42703", StringComparison.Ordinal)
                || msg.Contains("SnapshotJson", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("ExpiresAt", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<IReadOnlyList<AttemptQuestion>> ListQuestionsAsync(
        int attemptId,
        CancellationToken ct) =>
        await _db.Set<AttemptQuestion>()
            .Where(x => x.AttemptId == attemptId)
            .OrderBy(x => x.Order)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> ListLatestQuestionIdsAsync(
        int userId,
        string mode,
        CancellationToken ct)
    {
        var attemptId = await _db.Set<Attempt>()
            .Where(x => x.UserId == userId && x.Mode == mode)
            .OrderByDescending(x => x.StartedAt)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (attemptId is null)
        {
            return [];
        }

        return await _db.Set<AttemptQuestion>()
            .Where(x => x.AttemptId == attemptId.Value)
            .OrderBy(x => x.Order)
            .Select(x => x.QuestionId)
            .ToListAsync(ct);
    }

    public Task<AttemptAnswer?> FindAnswerAsync(
        int attemptId,
        int questionId,
        CancellationToken ct) =>
        _db.Set<AttemptAnswer>().FirstOrDefaultAsync(
            x => x.AttemptId == attemptId && x.QuestionId == questionId,
            ct);

    public async Task AddAnswerAsync(AttemptAnswer answer, CancellationToken ct) =>
        await _db.Set<AttemptAnswer>().AddAsync(answer, ct);

    public async Task<IReadOnlyList<AttemptAnswer>> ListAnswersAsync(
        int attemptId,
        CancellationToken ct) =>
        await _db.Set<AttemptAnswer>()
            .Where(x => x.AttemptId == attemptId)
            .ToListAsync(ct);

    public Task<int> CountAnswersAsync(CancellationToken ct) =>
        _db.Set<AttemptAnswer>().CountAsync(ct);

    public Task<AttemptRating?> FindRatingAsync(int attemptId, CancellationToken ct) =>
        _db.Set<AttemptRating>().FirstOrDefaultAsync(
            x => x.AttemptId == attemptId,
            ct);

    public Task<AttemptRating?> GetRatingByIdAsync(int id, CancellationToken ct) =>
        _db.Set<AttemptRating>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddRatingAsync(AttemptRating rating, CancellationToken ct) =>
        await _db.Set<AttemptRating>().AddAsync(rating, ct);

    public async Task<IReadOnlyList<AttemptRating>> ListRatingsAsync(
        CancellationToken ct) =>
        await _db.Set<AttemptRating>()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Attempt>> ListByUserAsync(
        int userId,
        CancellationToken ct) =>
        await _db.Set<Attempt>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Attempt>> ListByUsersAsync(
        IReadOnlyList<int> userIds,
        CancellationToken ct) =>
        await _db.Set<Attempt>()
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Attempt>> ListFinishedAsync(
        CancellationToken ct) =>
        await _db.Set<Attempt>()
            .AsNoTracking()
            .Where(x => x.FinishedAt != null)
            .OrderByDescending(x => x.FinishedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AttemptResultRow>> ListResultRowsAsync(
        int? userId,
        IReadOnlyCollection<int>? userIds,
        int maxRows,
        CancellationToken ct)
    {
        if (userIds is { Count: 0 })
        {
            return [];
        }

        var query = _db.Set<Attempt>()
            .AsNoTracking()
            .Where(x => x.FinishedAt != null);
        if (userId is not null)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }
        else if (userIds is not null)
        {
            var ids = userIds.Distinct().ToList();
            query = query.Where(x => ids.Contains(x.UserId));
        }

        return await query
            .OrderByDescending(x => x.FinishedAt)
            .ThenByDescending(x => x.Id)
            .Take(maxRows)
            .Select(x => new AttemptResultRow(
                x.Id,
                x.UserId,
                x.Mode,
                x.Percent,
                x.Passed,
                x.TimeSeconds,
                x.StartedAt,
                x.FinishedAt!.Value))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AttemptProgressRow>> ListFinishedForProgressAsync(
        int userId,
        IReadOnlyCollection<string>? modes,
        int maxRows,
        CancellationToken ct)
    {
        var query = _db.Set<Attempt>()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.FinishedAt != null);
        if (modes is { Count: > 0 })
        {
            query = query.Where(x => modes.Contains(x.Mode));
        }

        var latest = await query
            .OrderByDescending(x => x.FinishedAt)
            .ThenByDescending(x => x.Id)
            .Take(maxRows)
            .Select(x => new AttemptProgressRow(
                x.Id,
                x.Mode,
                x.TotalQuestions,
                x.CorrectCount,
                x.Percent,
                x.Passed,
                x.TimeSeconds,
                x.StartedAt,
                x.FinishedAt!.Value))
            .ToListAsync(ct);

        latest.Reverse();
        return latest;
    }

    public async Task<IReadOnlyDictionary<string, int>> CountFinishedByModeAsync(
        int userId,
        CancellationToken ct) =>
        await _db.Set<Attempt>()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.FinishedAt != null)
            .GroupBy(x => x.Mode)
            .Select(g => new { Mode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Mode, x => x.Count, ct);

    public async Task<IReadOnlyList<Attempt>> ListAllAsync(CancellationToken ct) =>
        await _db.Set<Attempt>()
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Attempt>> ListStartedSinceAsync(
        DateTime utcFrom,
        CancellationToken ct) =>
        await _db.Set<Attempt>()
            .AsNoTracking()
            .Where(x => x.StartedAt >= utcFrom)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(ct);

    public Task<int> CountAllAsync(CancellationToken ct) =>
        _db.Set<Attempt>().CountAsync(ct);

    public Task<int> CountFinishedByUserAndExamAsync(
        int userId,
        int examId,
        CancellationToken ct) =>
        _db.Set<Attempt>().CountAsync(
            x => x.UserId == userId && x.ExamId == examId && x.FinishedAt != null,
            ct);

    public Task<Attempt?> FindOpenByUserAndExamAsync(
        int userId,
        int examId,
        CancellationToken ct) =>
        _db.Set<Attempt>().FirstOrDefaultAsync(
            x => x.UserId == userId
                && x.ExamId == examId
                && x.FinishedAt == null,
            ct);

    public async Task<bool> TryMarkFinishedAsync(
        int attemptId,
        int correctCount,
        decimal percent,
        bool passed,
        int timeSeconds,
        DateTime finishedAt,
        CancellationToken ct)
    {
        var rows = await _db.Set<Attempt>()
            .Where(x => x.Id == attemptId && x.FinishedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.CorrectCount, correctCount)
                    .SetProperty(x => x.Percent, percent)
                    .SetProperty(x => x.Passed, passed)
                    .SetProperty(x => x.TimeSeconds, timeSeconds)
                    .SetProperty(x => x.FinishedAt, finishedAt),
                ct);
        return rows == 1;
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);

    public void ClearTrackedChanges() =>
        _db.ChangeTracker.Clear();
}
