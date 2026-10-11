using Cale.Modules.LiveClassroom.Domain;

namespace Cale.Modules.LiveClassroom.Application.Abstractions;

public interface ILiveSessionStore
{
    Task AddAsync(LiveSession session, CancellationToken ct = default);
    Task<LiveSession?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<LiveSession?> GetByJoinCodeAsync(string code, CancellationToken ct = default);
    Task<bool> JoinCodeExistsAsync(string code, CancellationToken ct = default);
    Task<LiveParticipant?> GetParticipantByTokenAsync(Guid token, CancellationToken ct = default);
    Task<LiveAnswer?> FindAnswerAsync(int sessionQuestionId, int participantId, CancellationToken ct = default);
    Task AddAnswerAsync(LiveAnswer answer, CancellationToken ct = default);
    Task<int> CountAnswersAsync(int sessionQuestionId, CancellationToken ct = default);
    Task<IReadOnlyList<LiveAnswer>> ListAnswersForQuestionAsync(int sessionQuestionId, CancellationToken ct = default);
    Task<IReadOnlyList<LiveAnswer>> ListAnswersForSessionAsync(int sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<int>> ListExpiredOpenSessionIdsAsync(DateTime utcNow, CancellationToken ct = default);
    /// <summary>Earliest deadline of a running question whose close has not been processed yet.</summary>
    Task<DateTime?> GetNextPendingCloseAsync(CancellationToken ct = default);
    /// <summary>
    /// Atomically marks the current question as closed. Returns false when another request,
    /// timer tick or instance already closed it (or the question changed), so the caller must
    /// not repeat the close side effects.
    /// </summary>
    Task<bool> TryClaimQuestionCloseAsync(
        int sessionId,
        int questionIndex,
        DateTime utcNow,
        bool onlyIfExpired,
        CancellationToken ct = default);
    Task<LiveParticipant?> GetParticipantByConnectionIdAsync(string connectionId, CancellationToken ct = default);
    Task AddDoubtAsync(LiveDoubt doubt, CancellationToken ct = default);
    Task<LiveDoubt?> GetDoubtAsync(int doubtId, CancellationToken ct = default);
    Task<LiveDoubtVote?> FindDoubtVoteAsync(int doubtId, int participantId, CancellationToken ct = default);
    Task AddDoubtVoteAsync(LiveDoubtVote vote, CancellationToken ct = default);
    Task<IReadOnlyList<LiveDoubt>> ListDoubtsAsync(int sessionId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Wakes the question timer when a deadline is created or resumed in this process.</summary>
public interface ILiveQuestionTimerSignal
{
    void Notify();
}

public interface ILiveSessionBroadcaster
{
    Task LobbyUpdatedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task QuestionStartedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task QuestionClosedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task AnswerReceivedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task SessionEndedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task RevealUpdatedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task AnswerRosterUpdatedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task RankingUpdatedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task DoubtsUpdatedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task RematchReadyAsync(int sessionId, object payload, CancellationToken ct = default);
    Task SurpriseQueuedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task PresentationSlideChangedAsync(int sessionId, object payload, CancellationToken ct = default);
}
