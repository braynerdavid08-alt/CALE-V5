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
    Task<IReadOnlyList<LiveAnswer>> ListAnswersForSessionAsync(int sessionId, CancellationToken ct = default);
    Task AddDoubtAsync(LiveDoubt doubt, CancellationToken ct = default);
    Task<LiveDoubt?> GetDoubtAsync(int doubtId, CancellationToken ct = default);
    Task<LiveDoubtVote?> FindDoubtVoteAsync(int doubtId, int participantId, CancellationToken ct = default);
    Task AddDoubtVoteAsync(LiveDoubtVote vote, CancellationToken ct = default);
    Task<IReadOnlyList<LiveDoubt>> ListDoubtsAsync(int sessionId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ILiveSessionBroadcaster
{
    Task LobbyUpdatedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task QuestionStartedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task QuestionClosedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task AnswerReceivedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task SessionEndedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task RevealUpdatedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task RankingUpdatedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task DoubtsUpdatedAsync(int sessionId, object payload, CancellationToken ct = default);
    Task RematchReadyAsync(int sessionId, object payload, CancellationToken ct = default);
    Task SurpriseQueuedAsync(int sessionId, object payload, CancellationToken ct = default);
}
