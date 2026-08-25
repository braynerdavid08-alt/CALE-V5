using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Classroom.Application.Abstractions;
using Cale.Modules.Classroom.Application.DTOs;
using Cale.Modules.Classroom.Domain;

namespace Cale.Modules.Classroom.Application.Queries;

public sealed class ClassroomQueryHandler
{
    private readonly IClassroomStore _store;
    private readonly IUserLookup _users;
    private readonly IAttemptStats _attempts;
    private readonly ICatalogStore _catalog;
    private readonly INotificationQueries _notifications;
    private readonly IClock _clock;

    public ClassroomQueryHandler(
        IClassroomStore store,
        IUserLookup users,
        IAttemptStats attempts,
        ICatalogStore catalog,
        INotificationQueries notifications,
        IClock clock)
    {
        _store = store;
        _users = users;
        _attempts = attempts;
        _catalog = catalog;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<IReadOnlyList<GroupDto>> ListGroupsAsync(
        int userId,
        string role,
        CancellationToken ct)
    {
        var groups = role == Roles.Admin
            ? await _store.ListAllAsync(ct)
            : role == Roles.Teacher
                ? await _store.ListByTeacherAsync(userId, ct)
                : await StudentGroups(userId, ct);

        var result = new List<GroupDto>();
        foreach (var group in groups)
        {
            result.Add(await MapGroup(group, ct));
        }

        return result;
    }

    public async Task<GroupDto> GetGroupAsync(
        int id,
        int userId,
        string role,
        CancellationToken ct)
    {
        var group = await _store.GetGroupAsync(id, ct)
            ?? throw new NotFoundException("Group not found.", "group_not_found");
        await EnsureCanView(group, userId, role, ct);
        return await MapGroup(group, ct);
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(
        int groupId,
        int userId,
        string role,
        CancellationToken ct)
    {
        var group = await _store.GetGroupAsync(groupId, ct)
            ?? throw new NotFoundException("Group not found.", "group_not_found");
        await EnsureCanView(group, userId, role, ct);
        var members = await _store.ListMembersAsync(groupId, ct);
        var result = new List<MemberDto>();
        foreach (var member in members.Where(x => x.IsActive))
        {
            var name = await _users.GetNameAsync(member.UserId, ct) ?? "";
            result.Add(new MemberDto(member.UserId, name, "", member.Status));
        }

        return result;
    }

    public async Task<IReadOnlyList<AnnouncementDto>> ListAnnouncementsAsync(
        int groupId,
        int userId,
        string role,
        CancellationToken ct)
    {
        var group = await _store.GetGroupAsync(groupId, ct)
            ?? throw new NotFoundException("Group not found.", "group_not_found");
        await EnsureCanView(group, userId, role, ct);
        var items = await _store.ListAnnouncementsAsync(groupId, ct);
        var result = new List<AnnouncementDto>();
        foreach (var item in items.Where(x => x.IsActive))
        {
            var name = await _users.GetNameAsync(item.AuthorId, ct) ?? "";
            result.Add(new AnnouncementDto(
                item.Id,
                item.Title,
                item.Body,
                item.AuthorId,
                name,
                item.CreatedAt));
        }

        return result;
    }

    public async Task<IReadOnlyList<MaterialDto>> ListMaterialsAsync(
        int groupId,
        int userId,
        string role,
        CancellationToken ct)
    {
        var group = await _store.GetGroupAsync(groupId, ct)
            ?? throw new NotFoundException("Group not found.", "group_not_found");
        await EnsureCanView(group, userId, role, ct);
        var items = await _store.ListMaterialsAsync(groupId, ct);
        return items.Where(x => x.IsActive).Select(x => new MaterialDto(
            x.Id,
            x.Module,
            x.Title,
            x.Description,
            x.Type,
            x.Url,
            x.TextContent,
            x.CreatedAt)).ToList();
    }

    public async Task<IReadOnlyList<ActivityDto>> ListActivitiesAsync(
        int groupId,
        int userId,
        string role,
        CancellationToken ct)
    {
        var group = await _store.GetGroupAsync(groupId, ct)
            ?? throw new NotFoundException("Group not found.", "group_not_found");
        await EnsureCanView(group, userId, role, ct);
        var items = await _store.ListActivitiesAsync(groupId, ct);
        var result = new List<ActivityDto>();
        foreach (var item in items.Where(x => x.IsActive))
        {
            var submission = await _store.FindSubmissionAsync(item.Id, userId, ct);
            result.Add(MapActivity(item, submission));
        }

        return result;
    }

    public async Task<IReadOnlyList<SubmissionDto>> ListSubmissionsAsync(
        int activityId,
        int userId,
        string role,
        CancellationToken ct)
    {
        var activity = await _store.GetActivityAsync(activityId, ct)
            ?? throw new NotFoundException("Activity not found.", "activity_not_found");
        var group = await _store.GetGroupAsync(activity.GroupId, ct)
            ?? throw new NotFoundException("Group not found.", "group_not_found");
        if (!group.CanManage(userId, role == Roles.Admin))
        {
            throw new ForbiddenException("You cannot view submissions.");
        }

        var items = await _store.ListSubmissionsAsync(activityId, ct);
        var result = new List<SubmissionDto>();
        foreach (var item in items)
        {
            var name = await _users.GetNameAsync(item.UserId, ct) ?? "";
            result.Add(MapSubmission(item, name));
        }

        return result;
    }

    public async Task<StudentDashboardDto> StudentDashboardAsync(
        int userId,
        string name,
        CancellationToken ct)
    {
        var groups = await StudentGroups(userId, ct);
        var groupDtos = new List<GroupDto>();
        var announcements = new List<AnnouncementDto>();
        var pending = new List<ActivityDto>();
        foreach (var group in groups)
        {
            groupDtos.Add(await MapGroup(group, ct));
            var avisos = await _store.ListAnnouncementsAsync(group.Id, ct);
            foreach (var aviso in avisos.Where(x => x.IsActive).Take(5))
            {
                var author = await _users.GetNameAsync(aviso.AuthorId, ct) ?? "";
                announcements.Add(new AnnouncementDto(
                    aviso.Id,
                    aviso.Title,
                    aviso.Body,
                    aviso.AuthorId,
                    author,
                    aviso.CreatedAt));
            }

            var activities = await _store.ListActivitiesAsync(group.Id, ct);
            foreach (var activity in activities.Where(x => x.IsActive))
            {
                var submission = await _store.FindSubmissionAsync(activity.Id, userId, ct);
                var dto = MapActivity(activity, submission);
                if (dto.Status is "Available" or "Pending" or "Expired")
                {
                    pending.Add(dto);
                }
            }
        }

        return new StudentDashboardDto(
            name,
            groupDtos,
            pending,
            announcements.OrderByDescending(x => x.CreatedAt).Take(8).ToList(),
            await _notifications.CountUnreadAsync(userId, ct),
            await _attempts.BestPercentAsync(userId, ct));
    }

    public async Task<TeacherDashboardDto> TeacherDashboardAsync(
        int userId,
        CancellationToken ct)
    {
        var groups = await _store.ListByTeacherAsync(userId, ct);
        var groupDtos = new List<GroupDto>();
        var activityIds = new List<int>();
        var memberIds = new List<int>();
        foreach (var group in groups)
        {
            groupDtos.Add(await MapGroup(group, ct));
            var activities = await _store.ListActivitiesAsync(group.Id, ct);
            activityIds.AddRange(activities.Select(x => x.Id));
            var members = await _store.ListMembersAsync(group.Id, ct);
            memberIds.AddRange(members.Where(x => x.IsActive).Select(x => x.UserId));
        }

        var ungraded = await _store.ListUngradedAsync(activityIds, ct);
        var pending = new List<SubmissionDto>();
        foreach (var item in ungraded)
        {
            var name = await _users.GetNameAsync(item.UserId, ct) ?? "";
            pending.Add(MapSubmission(item, name));
        }

        var attempts = memberIds.Count == 0
            ? []
            : await _attempts.ListByUsersAsync(memberIds.Distinct().ToList(), ct);
        var low = new List<ResultHintDto>();
        foreach (var attempt in attempts.Where(x => x.FinishedAt is not null && !x.Passed)
                     .Take(10))
        {
            var name = await _users.GetNameAsync(attempt.UserId, ct) ?? "";
            low.Add(new ResultHintDto(
                attempt.UserId,
                name,
                attempt.Percent,
                attempt.Passed));
        }

        return new TeacherDashboardDto(groupDtos, pending, low);
    }

    public async Task<AdminDashboardDto> AdminDashboardAsync(CancellationToken ct)
    {
        var banks = await _catalog.ListBanksAsync(false, ct);
        var questionCount = 0;
        foreach (var bank in banks)
        {
            questionCount += await _catalog.CountQuestionsInBankAsync(bank.Id, ct);
        }

        return new AdminDashboardDto(
            await _users.CountAsync(ct),
            await _store.CountGroupsAsync(ct),
            await _attempts.CountAllAsync(ct),
            questionCount,
            await _attempts.CountRatingsAsync(ct));
    }

    private async Task<IReadOnlyList<Group>> StudentGroups(
        int userId,
        CancellationToken ct)
    {
        var memberships = await _store.ListMembershipsAsync(userId, ct);
        var result = new List<Group>();
        foreach (var membership in memberships.Where(x => x.IsActive))
        {
            var group = await _store.GetGroupAsync(membership.GroupId, ct);
            if (group is { IsActive: true })
            {
                result.Add(group);
            }
        }

        return result;
    }

    private async Task EnsureCanView(
        Group group,
        int userId,
        string role,
        CancellationToken ct)
    {
        if (group.CanManage(userId, role == Roles.Admin))
        {
            return;
        }

        var member = await _store.FindMemberAsync(group.Id, userId, ct);
        if (member is null || !member.IsActive)
        {
            throw new ForbiddenException("You cannot view this group.");
        }
    }

    private async Task<GroupDto> MapGroup(Group group, CancellationToken ct)
    {
        var teacher = group.TeacherId is null
            ? null
            : await _users.GetNameAsync(group.TeacherId.Value, ct);
        var members = await _store.ListMembersAsync(group.Id, ct);
        return new GroupDto(
            group.Id,
            group.Name,
            group.Code,
            group.TeacherId,
            teacher,
            group.Description,
            group.StartsOn,
            group.IsActive,
            members.Count(x => x.IsActive));
    }

    private ActivityDto MapActivity(GroupActivity item, ActivitySubmission? submission) =>
        new(
            item.Id,
            item.Type,
            item.Title,
            item.Description,
            item.Instructions,
            item.DueAt,
            item.MaxScore,
            item.ResolveStatus(_clock.UtcNow, submission?.Status),
            submission?.Score,
            submission?.TeacherComment);

    private static SubmissionDto MapSubmission(ActivitySubmission item, string name) =>
        new(
            item.Id,
            item.ActivityId,
            item.UserId,
            name,
            item.TextContent,
            item.FileUrl,
            item.SubmittedAt,
            item.Score,
            item.TeacherComment,
            item.Status);
}
