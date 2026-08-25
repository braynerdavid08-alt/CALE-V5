using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Engagement.Application.Abstractions;
using Cale.Modules.Engagement.Application.DTOs;

namespace Cale.Modules.Engagement.Application.Queries;

public sealed class ListNotificationsHandler
{
    private readonly INotificationStore _store;

    public ListNotificationsHandler(INotificationStore store) => _store = store;

    public async Task<IReadOnlyList<NotificationDto>> HandleAsync(
        int userId,
        CancellationToken ct)
    {
        var items = await _store.ListByUserAsync(userId, ct);
        return items.Select(x => new NotificationDto(
            x.Id,
            x.Title,
            x.Message,
            x.Type,
            x.IsRead,
            x.CreatedAt,
            x.GroupId)).ToList();
    }

    public async Task MarkReadAsync(int id, int userId, CancellationToken ct)
    {
        var item = await _store.GetAsync(id, ct)
            ?? throw new NotFoundException(
                "Notification not found.",
                "notification_not_found");
        if (item.UserId != userId)
        {
            throw new ForbiddenException("This notification is not yours.");
        }

        item.MarkRead();
        await _store.SaveChangesAsync(ct);
    }
}
