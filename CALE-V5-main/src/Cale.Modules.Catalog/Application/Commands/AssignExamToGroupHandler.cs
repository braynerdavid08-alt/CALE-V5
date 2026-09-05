using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Catalog.Application.Commands;

public sealed class AssignExamToGroupHandler
{
    private readonly ICatalogStore _store;
    private readonly IGroupAccess _groups;
    private readonly INotificationPublisher _notifications;
    private readonly IClock _clock;

    public AssignExamToGroupHandler(
        ICatalogStore store,
        IGroupAccess groups,
        INotificationPublisher notifications,
        IClock clock)
    {
        _store = store;
        _groups = groups;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task HandleAsync(
        int examId,
        AssignExamToGroupRequest request,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var exam = await _store.GetExamAsync(examId, ct)
            ?? throw new NotFoundException("Exam not found.", "exam_not_found");
        if (!exam.IsActive)
        {
            throw new NotFoundException("Exam not found.", "exam_not_found");
        }

        if (!exam.CanEdit(userId, isAdmin))
        {
            throw new ForbiddenException("You cannot assign this exam.");
        }

        if (!await _groups.CanManageGroupAsync(request.GroupId, userId, isAdmin, ct))
        {
            throw new ForbiddenException(
                "Solo puedes asignar exámenes a tus propios grupos.",
                "group_forbidden");
        }

        if (await _store.FindExamGroupAsync(examId, request.GroupId, ct) is not null)
        {
            throw new ConflictException(
                "Exam already assigned to this group.",
                "exam_group_exists");
        }

        var link = ExamGroupLink.Create(
            examId,
            request.GroupId,
            request.StartsAt ?? exam.StartsAt,
            request.EndsAt ?? exam.EndsAt,
            _clock.UtcNow);
        await _store.AddExamGroupAsync(link, ct);
        await _store.SaveChangesAsync(ct);

        var members = await _groups.GetActiveMemberIdsAsync(request.GroupId, ct);
        await _notifications.NotifyUsersAsync(
            members,
            "Nuevo examen",
            $"Se programó el examen «{exam.Name}».",
            NotificationTypes.Exam,
            request.GroupId,
            "exam",
            exam.Id,
            ct);
    }
}
