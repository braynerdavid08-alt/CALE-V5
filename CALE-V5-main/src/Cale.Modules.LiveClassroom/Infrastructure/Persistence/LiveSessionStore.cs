using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.LiveClassroom.Application.Abstractions;
using Cale.Modules.LiveClassroom.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.LiveClassroom.Infrastructure.Persistence;

public sealed class LiveSessionStore : ILiveSessionStore
{
    private readonly CaleDbContext _db;

    public LiveSessionStore(CaleDbContext db) => _db = db;

    public async Task AddAsync(LiveSession session, CancellationToken ct = default) =>
        await _db.Set<LiveSession>().AddAsync(session, ct);

    public Task<LiveSession?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Set<LiveSession>()
            .Include(x => x.Participants)
            .Include(x => x.Questions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<LiveSession?> GetByJoinCodeAsync(string code, CancellationToken ct = default) =>
        _db.Set<LiveSession>()
            .Include(x => x.Participants)
            .Include(x => x.Questions)
            .FirstOrDefaultAsync(x => x.JoinCode == code, ct);

    public Task<bool> JoinCodeExistsAsync(string code, CancellationToken ct = default) =>
        _db.Set<LiveSession>().AnyAsync(x => x.JoinCode == code, ct);

    public Task<LiveParticipant?> GetParticipantByTokenAsync(
        Guid token,
        CancellationToken ct = default) =>
        _db.Set<LiveParticipant>().FirstOrDefaultAsync(x => x.ParticipantToken == token, ct);

    public Task<LiveAnswer?> FindAnswerAsync(
        int sessionQuestionId,
        int participantId,
        CancellationToken ct = default) =>
        _db.Set<LiveAnswer>().FirstOrDefaultAsync(
            x => x.SessionQuestionId == sessionQuestionId && x.ParticipantId == participantId,
            ct);

    public async Task AddAnswerAsync(LiveAnswer answer, CancellationToken ct = default) =>
        await _db.Set<LiveAnswer>().AddAsync(answer, ct);

    public Task<int> CountAnswersAsync(int sessionQuestionId, CancellationToken ct = default) =>
        _db.Set<LiveAnswer>().CountAsync(x => x.SessionQuestionId == sessionQuestionId, ct);

    public async Task<IReadOnlyList<LiveAnswer>> ListAnswersForSessionAsync(
        int sessionId,
        CancellationToken ct = default)
    {
        var questionIds = await _db.Set<LiveSessionQuestion>()
            .Where(q => q.SessionId == sessionId)
            .Select(q => q.Id)
            .ToListAsync(ct);
        if (questionIds.Count == 0)
        {
            return [];
        }

        return await _db.Set<LiveAnswer>()
            .Where(a => questionIds.Contains(a.SessionQuestionId))
            .ToListAsync(ct);
    }

    public async Task AddDoubtAsync(LiveDoubt doubt, CancellationToken ct = default) =>
        await _db.Set<LiveDoubt>().AddAsync(doubt, ct);

    public Task<LiveDoubt?> GetDoubtAsync(int doubtId, CancellationToken ct = default) =>
        _db.Set<LiveDoubt>()
            .Include(d => d.Votes)
            .FirstOrDefaultAsync(d => d.Id == doubtId, ct);

    public Task<LiveDoubtVote?> FindDoubtVoteAsync(
        int doubtId,
        int participantId,
        CancellationToken ct = default) =>
        _db.Set<LiveDoubtVote>().FirstOrDefaultAsync(
            v => v.DoubtId == doubtId && v.ParticipantId == participantId,
            ct);

    public async Task AddDoubtVoteAsync(LiveDoubtVote vote, CancellationToken ct = default) =>
        await _db.Set<LiveDoubtVote>().AddAsync(vote, ct);

    public async Task<IReadOnlyList<LiveDoubt>> ListDoubtsAsync(
        int sessionId,
        CancellationToken ct = default) =>
        await _db.Set<LiveDoubt>()
            .Include(d => d.Votes)
            .Where(d => d.SessionId == sessionId)
            .OrderByDescending(d => d.VoteCount)
            .ThenByDescending(d => d.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);
}
