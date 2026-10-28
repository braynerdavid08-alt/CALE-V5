using Cale.BuildingBlocks.Domain.Classroom;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Classroom.Application.Abstractions;
using Cale.Modules.Classroom.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Classroom.Infrastructure.Persistence;

public sealed class ClassroomStore : IClassroomStore
{
    private readonly CaleDbContext _db;

    public ClassroomStore(CaleDbContext db) => _db = db;

    public async Task<IReadOnlyList<Group>> ListAllAsync(CancellationToken ct) =>
        await _db.Set<Group>().OrderBy(x => x.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Group>> ListByTeacherAsync(
        int teacherId,
        CancellationToken ct) =>
        await _db.Set<Group>()
            .Where(x => x.TeacherId == teacherId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Group>> ListActiveByTeacherIdsAsync(
        IReadOnlyCollection<int> teacherIds,
        CancellationToken ct)
    {
        if (teacherIds.Count == 0)
        {
            return [];
        }

        return await _db.Set<Group>()
            .Where(x => x.IsActive
                && x.TeacherId != null
                && teacherIds.Contains(x.TeacherId.Value))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public Task<Group?> GetGroupAsync(int id, CancellationToken ct) =>
        _db.Set<Group>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Group?> FindByCodeAsync(string code, CancellationToken ct) =>
        _db.Set<Group>().FirstOrDefaultAsync(
            x => x.Code.ToUpper() == code.ToUpper(),
            ct);

    public async Task AddGroupAsync(Group group, CancellationToken ct) =>
        await _db.Set<Group>().AddAsync(group, ct);

    public Task<GroupMember?> FindMemberAsync(
        int groupId,
        int userId,
        CancellationToken ct) =>
        _db.Set<GroupMember>().FirstOrDefaultAsync(
            x => x.GroupId == groupId && x.UserId == userId,
            ct);

    public async Task<IReadOnlyList<GroupMember>> ListMembersAsync(
        int groupId,
        CancellationToken ct) =>
        await _db.Set<GroupMember>()
            .Where(x => x.GroupId == groupId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<GroupMember>> ListMembershipsAsync(
        int userId,
        CancellationToken ct) =>
        await _db.Set<GroupMember>()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

    public async Task AddMemberAsync(GroupMember member, CancellationToken ct) =>
        await _db.Set<GroupMember>().AddAsync(member, ct);

    public async Task EnsureActiveMemberAsync(
        int groupId,
        int userId,
        DateTime utcNow,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (_db.Database.IsNpgsql())
        {
            // Serialize concurrent joins for the same student+group.
            var lockKey = HashCode.Combine(groupId, userId);
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(@p0)",
                [lockKey],
                ct);
        }

        var member = await _db.Set<GroupMember>()
            .FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserId == userId, ct);
        if (member is null)
        {
            await _db.Set<GroupMember>().AddAsync(GroupMember.Join(groupId, userId, utcNow), ct);
        }
        else if (!member.IsActive)
        {
            member.Reactivate();
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            // Another request won the insert — treat as success if the row exists.
            var again = await FindMemberAsync(groupId, userId, ct);
            if (again is null)
            {
                throw;
            }

            if (!again.IsActive)
            {
                again.Reactivate();
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    public Task<int> CountActiveMembersAsync(int groupId, CancellationToken ct) =>
        _db.Set<GroupMember>().CountAsync(
            x => x.GroupId == groupId
                && (x.Status == MemberStatuses.Active || x.Status == "Active"),
            ct);

    public async Task<IReadOnlyDictionary<int, int>> CountActiveMembersByGroupsAsync(
        IReadOnlyCollection<int> groupIds,
        CancellationToken ct)
    {
        if (groupIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var rows = await _db.Set<GroupMember>()
            .Where(x => groupIds.Contains(x.GroupId)
                && (x.Status == MemberStatuses.Active || x.Status == "Active"))
            .GroupBy(x => x.GroupId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(x => x.Key, x => x.Count);
    }

    public async Task<IReadOnlyList<Announcement>> ListAnnouncementsForGroupsAsync(
        IReadOnlyCollection<int> groupIds,
        CancellationToken ct)
    {
        if (groupIds.Count == 0)
        {
            return [];
        }

        return await _db.Set<Announcement>()
            .Where(x => groupIds.Contains(x.GroupId) && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GroupActivity>> ListActivitiesForGroupsAsync(
        IReadOnlyCollection<int> groupIds,
        CancellationToken ct)
    {
        if (groupIds.Count == 0)
        {
            return [];
        }

        return await _db.Set<GroupActivity>()
            .Where(x => groupIds.Contains(x.GroupId) && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, ActivitySubmission>> ListSubmissionsByActivitiesAsync(
        int userId,
        IReadOnlyCollection<int> activityIds,
        CancellationToken ct)
    {
        if (activityIds.Count == 0)
        {
            return new Dictionary<int, ActivitySubmission>();
        }

        var rows = await _db.Set<ActivitySubmission>()
            .Where(x => x.UserId == userId && activityIds.Contains(x.ActivityId))
            .ToListAsync(ct);
        return rows.ToDictionary(x => x.ActivityId);
    }

    public async Task<IReadOnlyList<Announcement>> ListAnnouncementsAsync(
        int groupId,
        CancellationToken ct) =>
        await _db.Set<Announcement>()
            .Where(x => x.GroupId == groupId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAnnouncementAsync(
        Announcement announcement,
        CancellationToken ct) =>
        await _db.Set<Announcement>().AddAsync(announcement, ct);

    public async Task<IReadOnlyList<Material>> ListMaterialsAsync(
        int groupId,
        CancellationToken ct) =>
        await _db.Set<Material>()
            .Where(x => x.GroupId == groupId)
            .OrderBy(x => x.Module)
            .ThenBy(x => x.Title)
            .ToListAsync(ct);

    public async Task AddMaterialAsync(Material material, CancellationToken ct) =>
        await _db.Set<Material>().AddAsync(material, ct);

    public async Task<IReadOnlyList<GroupActivity>> ListActivitiesAsync(
        int groupId,
        CancellationToken ct) =>
        await _db.Set<GroupActivity>()
            .Where(x => x.GroupId == groupId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public Task<GroupActivity?> GetActivityAsync(int id, CancellationToken ct) =>
        _db.Set<GroupActivity>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddActivityAsync(GroupActivity activity, CancellationToken ct) =>
        await _db.Set<GroupActivity>().AddAsync(activity, ct);

    public Task<ActivitySubmission?> FindSubmissionAsync(
        int activityId,
        int userId,
        CancellationToken ct) =>
        _db.Set<ActivitySubmission>().FirstOrDefaultAsync(
            x => x.ActivityId == activityId && x.UserId == userId,
            ct);

    public async Task<IReadOnlyList<ActivitySubmission>> ListSubmissionsAsync(
        int activityId,
        CancellationToken ct) =>
        await _db.Set<ActivitySubmission>()
            .Where(x => x.ActivityId == activityId)
            .OrderBy(x => x.SubmittedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ActivitySubmission>> ListUngradedAsync(
        IReadOnlyList<int> activityIds,
        CancellationToken ct)
    {
        if (activityIds.Count == 0)
        {
            return [];
        }

        return await _db.Set<ActivitySubmission>()
            .Where(x => activityIds.Contains(x.ActivityId)
                && x.Status != SubmissionStatuses.Graded)
            .OrderBy(x => x.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task AddSubmissionAsync(
        ActivitySubmission submission,
        CancellationToken ct) =>
        await _db.Set<ActivitySubmission>().AddAsync(submission, ct);

    public Task<int> CountGroupsAsync(CancellationToken ct) =>
        _db.Set<Group>().CountAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
