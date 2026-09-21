using Cale.Modules.GameShow.Application.DTOs;
using Cale.Modules.GameShow.Domain;

namespace Cale.Modules.GameShow.Application.Abstractions;

public interface IGameShowStore
{
    Task AddAsync(GameShowSession session, CancellationToken ct);
    Task<GameShowSession?> GetByIdAsync(int id, CancellationToken ct);
    Task<GameShowSession?> GetByJoinCodeAsync(string code, CancellationToken ct);
    Task<GameShowPlayer?> GetPlayerByTokenAsync(Guid token, CancellationToken ct);
    Task<bool> JoinCodeExistsAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<GameShowSession>> ListForHostAsync(int hostUserId, CancellationToken ct);
    Task<IReadOnlyList<GameShowSession>> ListForSchoolAsync(int schoolUserId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IGameShowBroadcaster
{
    Task LobbyUpdatedAsync(int sessionId, GameShowLobbyDto lobby, CancellationToken ct);
    Task EventAsync(int sessionId, string eventName, object payload, CancellationToken ct);
}
