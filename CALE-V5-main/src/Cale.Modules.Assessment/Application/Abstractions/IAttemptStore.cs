using Cale.Modules.Assessment.Domain;

namespace Cale.Modules.Assessment.Application.Abstractions;

public interface IAttemptStore
{
    Task<Attempt?> GetAsync(int id, CancellationToken ct);
    Task AddAsync(Attempt attempt, CancellationToken ct);
    Task AddQuestionsAsync(
        int attemptId,
        IReadOnlyList<AttemptQuestion> questions,
        CancellationToken ct);

    Task<IReadOnlyList<AttemptQuestion>> ListQuestionsAsync(
        int attemptId,
        CancellationToken ct);

    Task<AttemptAnswer?> FindAnswerAsync(
        int attemptId,
        int questionId,
        CancellationToken ct);

    Task AddAnswerAsync(AttemptAnswer answer, CancellationToken ct);

    Task<IReadOnlyList<AttemptAnswer>> ListAnswersAsync(
        int attemptId,
        CancellationToken ct);

    Task<AttemptRating?> FindRatingAsync(int attemptId, CancellationToken ct);
    Task<AttemptRating?> GetRatingByIdAsync(int id, CancellationToken ct);
    Task AddRatingAsync(AttemptRating rating, CancellationToken ct);
    Task<IReadOnlyList<AttemptRating>> ListRatingsAsync(CancellationToken ct);

    Task<IReadOnlyList<Attempt>> ListByUserAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<Attempt>> ListByUsersAsync(
        IReadOnlyList<int> userIds,
        CancellationToken ct);
    Task<IReadOnlyList<Attempt>> ListFinishedAsync(CancellationToken ct);

    Task<int> CountAllAsync(CancellationToken ct);
    Task<int> CountFinishedByUserAndExamAsync(
        int userId,
        int examId,
        CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
