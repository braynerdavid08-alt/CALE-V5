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

    public async Task<IReadOnlyList<AttemptQuestion>> ListQuestionsAsync(
        int attemptId,
        CancellationToken ct) =>
        await _db.Set<AttemptQuestion>()
            .Where(x => x.AttemptId == attemptId)
            .OrderBy(x => x.Order)
            .ToListAsync(ct);

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
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Attempt>> ListByUsersAsync(
        IReadOnlyList<int> userIds,
        CancellationToken ct) =>
        await _db.Set<Attempt>()
            .Where(x => userIds.Contains(x.UserId))
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Attempt>> ListFinishedAsync(
        CancellationToken ct) =>
        await _db.Set<Attempt>()
            .Where(x => x.FinishedAt != null)
            .OrderByDescending(x => x.FinishedAt)
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

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
