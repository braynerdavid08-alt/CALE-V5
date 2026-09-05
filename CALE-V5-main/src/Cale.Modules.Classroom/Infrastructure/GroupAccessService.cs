using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Classroom.Application.Abstractions;

namespace Cale.Modules.Classroom.Infrastructure;

public sealed class GroupAccessService : IGroupAccess
{
    private readonly IClassroomStore _store;

    public GroupAccessService(IClassroomStore store) => _store = store;

    public async Task<bool> IsActiveMemberAsync(
        int groupId,
        int userId,
        CancellationToken ct)
    {
        var member = await _store.FindMemberAsync(groupId, userId, ct);
        return member is { IsActive: true };
    }

    public async Task<IReadOnlyList<int>> GetActiveMemberIdsAsync(
        int groupId,
        CancellationToken ct)
    {
        var members = await _store.ListMembersAsync(groupId, ct);
        return members.Where(x => x.IsActive).Select(x => x.UserId).ToList();
    }

    public async Task<IReadOnlyList<int>> GetActiveGroupIdsAsync(
        int userId,
        CancellationToken ct)
    {
        var memberships = await _store.ListMembershipsAsync(userId, ct);
        return memberships.Where(x => x.IsActive).Select(x => x.GroupId).ToList();
    }

    public async Task<bool> CanManageGroupAsync(
        int groupId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        if (isAdmin)
        {
            return true;
        }

        var group = await _store.GetGroupAsync(groupId, ct);
        return group is not null && group.CanManage(userId, isAdmin: false);
    }
}
