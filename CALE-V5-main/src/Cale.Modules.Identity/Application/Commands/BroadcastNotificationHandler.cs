using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Abstractions;

namespace Cale.Modules.Identity.Application.Commands;

/// <summary>
/// Admin-only fan-out using existing user/group stores.
/// </summary>
public sealed class BroadcastNotificationHandler
{
    private readonly INotificationPublisher _notifications;
    private readonly IUserStore _users;
    private readonly IGroupAccess _groups;

    public BroadcastNotificationHandler(
        INotificationPublisher notifications,
        IUserStore users,
        IGroupAccess groups)
    {
        _notifications = notifications;
        _users = users;
        _groups = groups;
    }

    public async Task<BroadcastNotificationResponse> HandleAsync(
        BroadcastNotificationRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new DomainException(
                "Title and message are required.",
                400,
                "notification_invalid");
        }

        var type = string.IsNullOrWhiteSpace(request.Type)
            ? NotificationTypes.Admin
            : request.Type.Trim();
        if (type is not (
            NotificationTypes.Admin
            or NotificationTypes.System
            or NotificationTypes.Announcement))
        {
            throw new DomainException(
                "Broadcast type must be admin, system, or announcement.",
                400,
                "notification_type_invalid");
        }

        var audience = (request.Audience ?? "").Trim().ToLowerInvariant();
        var userIds = await ResolveAudienceAsync(audience, request, ct);
        if (userIds.Count == 0)
        {
            throw new DomainException(
                "No recipients matched the audience.",
                400,
                "notification_empty_audience");
        }

        await _notifications.NotifyUsersAsync(
            userIds,
            new NotificationDraft(
                request.Title.Trim(),
                request.Message.Trim(),
                type,
                request.GroupId,
                "broadcast",
                null,
                request.Link,
                string.IsNullOrWhiteSpace(request.Priority)
                    ? NotificationPriorities.Normal
                    : request.Priority!.Trim(),
                DedupeKey: null),
            ct);

        return new BroadcastNotificationResponse(userIds.Count, audience);
    }

    private async Task<IReadOnlyList<int>> ResolveAudienceAsync(
        string audience,
        BroadcastNotificationRequest request,
        CancellationToken ct)
    {
        switch (audience)
        {
            case "all_students":
            {
                var students = await _users.ListByRoleAsync(Roles.Student, ct);
                return students.Select(x => x.Id).ToList();
            }
            case "all_teachers":
            {
                var teachers = await _users.ListByRoleAsync(Roles.Teacher, ct);
                return teachers.Select(x => x.Id).ToList();
            }
            case "group":
            {
                if (request.GroupId is not > 0)
                {
                    throw new DomainException(
                        "GroupId is required for group audience.",
                        400,
                        "notification_group_required");
                }

                return await _groups.GetActiveMemberIdsAsync(request.GroupId.Value, ct);
            }
            case "users":
            {
                var ids = (request.UserIds ?? [])
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();
                if (ids.Count == 0)
                {
                    throw new DomainException(
                        "UserIds are required for users audience.",
                        400,
                        "notification_users_required");
                }

                return ids;
            }
            default:
                throw new DomainException(
                    "Audience must be all_students, all_teachers, group, or users.",
                    400,
                    "notification_audience_invalid");
        }
    }
}
