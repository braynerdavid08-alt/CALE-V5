using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.GameShow.Application.Abstractions;
using Cale.Modules.GameShow.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.GameShow.Infrastructure.Persistence;

public sealed class GameShowStore : IGameShowStore
{
    private readonly CaleDbContext _db;

    public GameShowStore(CaleDbContext db) => _db = db;

    public async Task AddAsync(GameShowSession session, CancellationToken ct) =>
        await _db.Set<GameShowSession>().AddAsync(session, ct);

    public Task<GameShowSession?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.Set<GameShowSession>()
            .AsSplitQuery()
            .Include(x => x.Rounds)
                .ThenInclude(r => r.Answers)
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<GameShowSession?> GetByIdWithAttemptsAsync(int id, CancellationToken ct) =>
        _db.Set<GameShowSession>()
            .AsSplitQuery()
            .Include(x => x.Rounds)
                .ThenInclude(r => r.Answers)
            .Include(x => x.Rounds)
                .ThenInclude(r => r.Attempts)
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<GameShowSession?> GetByJoinCodeAsync(string code, CancellationToken ct) =>
        _db.Set<GameShowSession>()
            .AsSplitQuery()
            .Include(x => x.Rounds)
                .ThenInclude(r => r.Answers)
            .Include(x => x.Players)
            .FirstOrDefaultAsync(x => x.JoinCode == code, ct);

    public Task<GameShowPlayer?> GetPlayerByTokenAsync(Guid token, CancellationToken ct) =>
        _db.Set<GameShowPlayer>().FirstOrDefaultAsync(x => x.PlayerToken == token, ct);

    public Task<GameShowPlayer?> GetPlayerByConnectionIdAsync(string connectionId, CancellationToken ct) =>
        _db.Set<GameShowPlayer>().FirstOrDefaultAsync(x => x.ConnectionId == connectionId, ct);

    public Task<bool> JoinCodeExistsAsync(string code, CancellationToken ct) =>
        _db.Set<GameShowSession>().AnyAsync(x => x.JoinCode == code, ct);

    public async Task<bool> TryClaimBuzzAsync(int roundId, string team, CancellationToken ct)
    {
        var updated = await _db.Set<GameShowRound>()
            .Where(r =>
                r.Id == roundId
                && r.Phase == GameShowRoundPhases.WaitingBuzz
                && r.BuzzWinnerTeam == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.BuzzWinnerTeam, team)
                    .SetProperty(r => r.ControllingTeam, team)
                    .SetProperty(r => r.Phase, GameShowRoundPhases.FaceOff)
                    .SetProperty(r => r.Strikes, 0)
                    .SetProperty(r => r.RoundPointsForController, 0)
                    .SetProperty(r => r.StealSucceeded, false),
                ct);
        return updated == 1;
    }

    public async Task<IReadOnlyList<GameShowSession>> ListForHostAsync(
        int hostUserId,
        CancellationToken ct) =>
        await _db.Set<GameShowSession>()
            .Include(x => x.Players)
            .Where(x => x.HostUserId == hostUserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<GameShowSession>> ListForSchoolAsync(
        int schoolUserId,
        CancellationToken ct) =>
        await _db.Set<GameShowSession>()
            .Include(x => x.Players)
            .Where(x => x.SchoolUserId == schoolUserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

    public async Task AddPackAsync(GameShowPack pack, CancellationToken ct) =>
        await _db.Set<GameShowPack>().AddAsync(pack, ct);

    public Task<GameShowPack?> GetPackByIdAsync(int id, CancellationToken ct) =>
        _db.Set<GameShowPack>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<GameShowPack>> ListPacksForOwnerAsync(
        int ownerUserId,
        CancellationToken ct) =>
        await _db.Set<GameShowPack>()
            .Where(x => x.OwnerUserId == ownerUserId)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(100)
            .ToListAsync(ct);

    public Task RemovePackAsync(GameShowPack pack, CancellationToken ct)
    {
        _db.Set<GameShowPack>().Remove(pack);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
