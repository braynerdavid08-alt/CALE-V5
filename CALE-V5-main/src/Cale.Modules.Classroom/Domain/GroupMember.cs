using Cale.BuildingBlocks.Domain.Classroom;

namespace Cale.Modules.Classroom.Domain;

public sealed class GroupMember
{
    public int Id { get; private set; }
    public int GroupId { get; private set; }
    public int UserId { get; private set; }
    public string Status { get; private set; } = MemberStatuses.Active;
    public DateTime CreatedAt { get; private set; }

    private GroupMember()
    {
    }

    public static GroupMember Join(int groupId, int userId, DateTime utcNow) =>
        new()
        {
            GroupId = groupId,
            UserId = userId,
            Status = MemberStatuses.Active,
            CreatedAt = utcNow
        };

    public void Remove() => Status = MemberStatuses.Removed;

    public void Reactivate() => Status = MemberStatuses.Active;

    public bool IsActive => Status == MemberStatuses.Active
        || string.Equals(Status, "Active", StringComparison.OrdinalIgnoreCase);
}
