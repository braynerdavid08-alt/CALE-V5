using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Classroom.Application.Abstractions;
using Cale.Modules.Classroom.Application.DTOs;
using Cale.Modules.Classroom.Domain;

namespace Cale.Modules.Classroom.Application.Commands;

public sealed class ClassroomContentHandler
{
    private readonly IClassroomStore _store;
    private readonly IGroupAccess _access;
    private readonly INotificationPublisher _notifications;
    private readonly IClock _clock;

    public ClassroomContentHandler(
        IClassroomStore store,
        IGroupAccess access,
        INotificationPublisher notifications,
        IClock clock)
    {
        _store = store;
        _access = access;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task PublishAnnouncementAsync(
        int groupId,
        SaveAnnouncementRequest request,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        await EnsureManage(groupId, userId, isAdmin, ct);
        var item = Announcement.Publish(
            groupId,
            userId,
            request.Title,
            request.Body,
            _clock.UtcNow);
        await _store.AddAnnouncementAsync(item, ct);
        await _store.SaveChangesAsync(ct);
        await Notify(
            groupId,
            "Nuevo aviso",
            item.Title,
            NotificationTypes.Announcement,
            "announcement",
            item.Id,
            ct);
    }

    public async Task PublishMaterialAsync(
        int groupId,
        SaveMaterialRequest request,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        await EnsureManage(groupId, userId, isAdmin, ct);
        var item = Material.Publish(
            groupId,
            userId,
            request.Module,
            request.Title,
            request.Description,
            request.Type,
            request.Url,
            request.TextContent,
            _clock.UtcNow);
        await _store.AddMaterialAsync(item, ct);
        await _store.SaveChangesAsync(ct);
        await Notify(
            groupId,
            "Nuevo material",
            item.Title,
            NotificationTypes.Material,
            "material",
            item.Id,
            ct);
    }

    public async Task PublishActivityAsync(
        int groupId,
        SaveActivityRequest request,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        await EnsureManage(groupId, userId, isAdmin, ct);
        var item = GroupActivity.Publish(
            groupId,
            userId,
            request.Type,
            request.Title,
            request.Description,
            request.Instructions,
            request.DueAt,
            request.MaxScore,
            _clock.UtcNow);
        await _store.AddActivityAsync(item, ct);
        await _store.SaveChangesAsync(ct);
        await Notify(
            groupId,
            "Nueva actividad",
            item.Title,
            NotificationTypes.Activity,
            "activity",
            item.Id,
            ct);
    }

    public async Task SubmitAsync(
        int activityId,
        SubmitActivityRequest request,
        int userId,
        CancellationToken ct)
    {
        var activity = await _store.GetActivityAsync(activityId, ct)
            ?? throw new NotFoundException("Activity not found.", "activity_not_found");
        if (!await _access.IsActiveMemberAsync(activity.GroupId, userId, ct))
        {
            throw new ForbiddenException("You are not in this group.");
        }

        if (await _store.FindSubmissionAsync(activityId, userId, ct) is not null)
        {
            throw new ConflictException(
                "You already submitted this activity.",
                "submission_exists");
        }

        var submission = ActivitySubmission.Deliver(
            activityId,
            userId,
            request.Text,
            request.FileUrl,
            _clock.UtcNow);
        await _store.AddSubmissionAsync(submission, ct);
        await _store.SaveChangesAsync(ct);

        var group = await _store.GetGroupAsync(activity.GroupId, ct);
        if (group?.TeacherId is { } teacherId)
        {
            await _notifications.NotifyUserAsync(
                teacherId,
                "Nueva entrega",
                $"Entrega en «{activity.Title}».",
                NotificationTypes.Submission,
                activity.GroupId,
                "activity",
                activity.Id,
                ct);
        }
    }

    public async Task GradeAsync(
        int activityId,
        int studentId,
        GradeSubmissionRequest request,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var activity = await _store.GetActivityAsync(activityId, ct)
            ?? throw new NotFoundException("Activity not found.", "activity_not_found");
        await EnsureManage(activity.GroupId, userId, isAdmin, ct);
        var submission = await _store.FindSubmissionAsync(activityId, studentId, ct)
            ?? throw new NotFoundException(
                "Submission not found.",
                "submission_not_found");
        submission.Grade(request.Score, request.Comment);
        await _store.SaveChangesAsync(ct);
        await _notifications.NotifyUserAsync(
            studentId,
            "Actividad calificada",
            $"Tu entrega de «{activity.Title}» fue calificada.",
            NotificationTypes.Grade,
            activity.GroupId,
            "activity",
            activity.Id,
            ct);
    }

    private async Task EnsureManage(
        int groupId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var group = await _store.GetGroupAsync(groupId, ct)
            ?? throw new NotFoundException("Group not found.", "group_not_found");
        if (!group.CanManage(userId, isAdmin))
        {
            throw new ForbiddenException("You cannot manage this group.");
        }
    }

    private async Task Notify(
        int groupId,
        string title,
        string message,
        string type,
        string entity,
        int entityId,
        CancellationToken ct)
    {
        var members = await _access.GetActiveMemberIdsAsync(groupId, ct);
        await _notifications.NotifyUsersAsync(
            members,
            title,
            message,
            type,
            groupId,
            entity,
            entityId,
            ct);
    }
}
