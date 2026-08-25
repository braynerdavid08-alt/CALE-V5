using Cale.Modules.Classroom.Domain;

namespace Cale.Modules.Classroom.Application.Abstractions;

public interface IClassroomStore
{
    Task<IReadOnlyList<Group>> ListAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Group>> ListByTeacherAsync(int teacherId, CancellationToken ct);
    Task<Group?> GetGroupAsync(int id, CancellationToken ct);
    Task<Group?> FindByCodeAsync(string code, CancellationToken ct);
    Task AddGroupAsync(Group group, CancellationToken ct);

    Task<GroupMember?> FindMemberAsync(int groupId, int userId, CancellationToken ct);
    Task<IReadOnlyList<GroupMember>> ListMembersAsync(int groupId, CancellationToken ct);
    Task<IReadOnlyList<GroupMember>> ListMembershipsAsync(int userId, CancellationToken ct);
    Task AddMemberAsync(GroupMember member, CancellationToken ct);

    Task<IReadOnlyList<Announcement>> ListAnnouncementsAsync(
        int groupId,
        CancellationToken ct);
    Task AddAnnouncementAsync(Announcement announcement, CancellationToken ct);

    Task<IReadOnlyList<Material>> ListMaterialsAsync(int groupId, CancellationToken ct);
    Task AddMaterialAsync(Material material, CancellationToken ct);

    Task<IReadOnlyList<GroupActivity>> ListActivitiesAsync(
        int groupId,
        CancellationToken ct);
    Task<GroupActivity?> GetActivityAsync(int id, CancellationToken ct);
    Task AddActivityAsync(GroupActivity activity, CancellationToken ct);

    Task<ActivitySubmission?> FindSubmissionAsync(
        int activityId,
        int userId,
        CancellationToken ct);
    Task<IReadOnlyList<ActivitySubmission>> ListSubmissionsAsync(
        int activityId,
        CancellationToken ct);
    Task<IReadOnlyList<ActivitySubmission>> ListUngradedAsync(
        IReadOnlyList<int> activityIds,
        CancellationToken ct);
    Task AddSubmissionAsync(ActivitySubmission submission, CancellationToken ct);

    Task<int> CountGroupsAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
