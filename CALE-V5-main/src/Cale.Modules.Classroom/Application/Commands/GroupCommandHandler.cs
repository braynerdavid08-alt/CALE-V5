using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Classroom.Application.Abstractions;
using Cale.Modules.Classroom.Application.DTOs;
using Cale.Modules.Classroom.Domain;

namespace Cale.Modules.Classroom.Application.Commands;

public sealed class GroupCommandHandler
{
    private readonly IClassroomStore _store;
    private readonly IUserLookup _users;
    private readonly IClock _clock;

    public GroupCommandHandler(
        IClassroomStore store,
        IUserLookup users,
        IClock clock)
    {
        _store = store;
        _users = users;
        _clock = clock;
    }

    public async Task<GroupDto> CreateAsync(
        SaveGroupRequest request,
        int teacherId,
        CancellationToken ct)
    {
        var group = Group.Create(
            request.Name,
            request.Description,
            teacherId,
            request.StartsOn,
            _clock.UtcNow);
        group.SetActive(request.IsActive);
        await _store.AddGroupAsync(group, ct);
        await _store.SaveChangesAsync(ct);
        return await Map(group, ct);
    }

    public async Task<GroupDto> UpdateAsync(
        int id,
        SaveGroupRequest request,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var group = await Owned(id, userId, isAdmin, ct);
        group.Update(request.Name, request.Description, request.StartsOn);
        group.SetActive(request.IsActive);
        await _store.SaveChangesAsync(ct);
        return await Map(group, ct);
    }

    public async Task<GroupDto> JoinAsync(
        JoinGroupRequest request,
        int userId,
        CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var group = await _store.FindByCodeAsync(code, ct)
            ?? throw new NotFoundException("Group code not found.", "group_not_found");
        if (!group.IsActive)
        {
            throw new ForbiddenException("Group is inactive.");
        }

        await EnsureStudentCanJoinSchoolGroupAsync(userId, group, ct);

        var member = await _store.FindMemberAsync(group.Id, userId, ct);
        if (member is null)
        {
            await _store.AddMemberAsync(
                GroupMember.Join(group.Id, userId, _clock.UtcNow),
                ct);
        }
        else if (!member.IsActive)
        {
            member.Reactivate();
        }

        await _store.SaveChangesAsync(ct);
        return await Map(group, ct);
    }

    public async Task AddMemberAsync(
        int groupId,
        AddMemberRequest request,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var group = await Owned(groupId, userId, isAdmin, ct);
        var email = request.Email.Trim().ToLowerInvariant();
        var targetId = await _users.FindIdByEmailAsync(email, ct)
            ?? throw new NotFoundException("User not found.", "user_not_found");

        if (!isAdmin)
        {
            await EnsureStudentCanJoinSchoolGroupAsync(targetId, group, ct);
        }
        else
        {
            var targetSchool = await _users.GetSchoolIdAsync(targetId, ct);
            if (targetSchool is null)
            {
                throw new ForbiddenException(
                    "El estudiante debe estar vinculado a una escuela.",
                    "no_school");
            }
        }

        var member = await _store.FindMemberAsync(group.Id, targetId, ct);
        if (member is null)
        {
            await _store.AddMemberAsync(
                GroupMember.Join(group.Id, targetId, _clock.UtcNow),
                ct);
        }
        else
        {
            member.Reactivate();
        }

        await _store.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(
        int groupId,
        int memberUserId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var group = await Owned(groupId, userId, isAdmin, ct);
        var member = await _store.FindMemberAsync(group.Id, memberUserId, ct)
            ?? throw new NotFoundException("Member not found.", "member_not_found");
        member.Remove();
        await _store.SaveChangesAsync(ct);
    }

    private async Task EnsureStudentCanJoinSchoolGroupAsync(
        int studentUserId,
        Group group,
        CancellationToken ct)
    {
        var studentSchoolId = await _users.GetSchoolIdAsync(studentUserId, ct);
        if (studentSchoolId is null)
        {
            throw new ForbiddenException(
                "Debes estar vinculado a una escuela para unirte a un grupo.",
                "no_school");
        }

        if (group.TeacherId is not int teacherId)
        {
            return;
        }

        var teacherSchoolId = await _users.GetSchoolIdAsync(teacherId, ct);
        if (teacherSchoolId is null)
        {
            return;
        }

        if (teacherSchoolId != studentSchoolId)
        {
            throw new ForbiddenException(
                "Este grupo pertenece a otra escuela.",
                "group_wrong_school");
        }
    }

    private async Task<Group> Owned(
        int id,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var group = await _store.GetGroupAsync(id, ct)
            ?? throw new NotFoundException("Group not found.", "group_not_found");
        if (!group.CanManage(userId, isAdmin))
        {
            throw new ForbiddenException("You cannot manage this group.");
        }

        return group;
    }

    private async Task<GroupDto> Map(Group group, CancellationToken ct)
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
}
