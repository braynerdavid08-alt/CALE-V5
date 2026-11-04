namespace Cale.Modules.Identity.Application.Services;

/// <summary>
/// Phase 1: only the school account itself may manage its commercial profile.
/// Prepared for a future per-user grant system without changing Identity roles yet.
/// </summary>
public interface ISchoolCommercialAccessPolicy
{
    Task<bool> CanManageAsync(int actorUserId, int schoolUserId, CancellationToken ct = default);
}

public sealed class OwnerOnlySchoolCommercialAccessPolicy : ISchoolCommercialAccessPolicy
{
    public Task<bool> CanManageAsync(int actorUserId, int schoolUserId, CancellationToken ct = default) =>
        Task.FromResult(actorUserId > 0 && actorUserId == schoolUserId);
}
