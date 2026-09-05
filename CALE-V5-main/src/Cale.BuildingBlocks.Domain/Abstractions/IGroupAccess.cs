namespace Cale.BuildingBlocks.Domain.Abstractions;

public interface IGroupAccess
{
    Task<bool> IsActiveMemberAsync(
        int groupId,
        int userId,
        CancellationToken ct);

    Task<IReadOnlyList<int>> GetActiveMemberIdsAsync(
        int groupId,
        CancellationToken ct);

    Task<IReadOnlyList<int>> GetActiveGroupIdsAsync(
        int userId,
        CancellationToken ct);

    /// <summary>Admin or the group's assigned teacher.</summary>
    Task<bool> CanManageGroupAsync(
        int groupId,
        int userId,
        bool isAdmin,
        CancellationToken ct);
}
