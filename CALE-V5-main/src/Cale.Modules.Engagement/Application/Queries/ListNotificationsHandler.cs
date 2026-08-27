using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Engagement.Application.Abstractions;
using Cale.Modules.Engagement.Application.DTOs;
using Cale.Modules.Engagement.Domain;

namespace Cale.Modules.Engagement.Application.Queries;

public sealed class ListNotificationsHandler
{
    private readonly INotificationStore _store;
    private readonly IClock _clock;

    public ListNotificationsHandler(INotificationStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<NotificationListResponse> HandleAsync(
        int userId,
        bool? unreadOnly,
        string? category,
        string? type,
        int skip,
        int take,
        CancellationToken ct)
    {
        var items = await _store.ListByUserAsync(
            new NotificationListQuery(userId, unreadOnly, category, type, skip, take),
            ct);
        var unread = await _store.CountUnreadAsync(userId, ct);
        return new NotificationListResponse(
            items.Select(Map).ToList(),
            unread);
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct) =>
        _store.CountUnreadAsync(userId, ct);

    public async Task MarkReadAsync(int id, int userId, CancellationToken ct)
    {
        var item = await RequireOwnedAsync(id, userId, ct);
        item.MarkRead(_clock.UtcNow);
        await _store.SaveChangesAsync(ct);
    }

    public async Task<int> MarkAllReadAsync(int userId, CancellationToken ct)
    {
        var count = await _store.MarkAllReadAsync(userId, _clock.UtcNow, ct);
        await _store.SaveChangesAsync(ct);
        return count;
    }

    public async Task ArchiveAsync(int id, int userId, CancellationToken ct)
    {
        var item = await RequireOwnedAsync(id, userId, ct);
        item.Archive();
        await _store.SaveChangesAsync(ct);
    }

    public async Task<NotificationPreferenceDto> GetPreferencesAsync(
        int userId,
        CancellationToken ct)
    {
        var prefs = await _store.GetPreferenceAsync(userId, ct)
            ?? NotificationPreference.Defaults(userId);
        return new NotificationPreferenceDto(
            prefs.AcademicEnabled,
            prefs.MembershipEnabled,
            prefs.AdminEnabled,
            prefs.SystemEnabled);
    }

    public async Task<NotificationPreferenceDto> UpdatePreferencesAsync(
        int userId,
        UpdateNotificationPreferenceRequest request,
        CancellationToken ct)
    {
        var prefs = await _store.GetPreferenceAsync(userId, ct);
        if (prefs is null)
        {
            prefs = NotificationPreference.Defaults(userId);
            await _store.AddPreferenceAsync(prefs, ct);
        }

        prefs.Update(
            request.AcademicEnabled,
            request.MembershipEnabled,
            request.AdminEnabled,
            request.SystemEnabled);
        await _store.SaveChangesAsync(ct);
        return new NotificationPreferenceDto(
            prefs.AcademicEnabled,
            prefs.MembershipEnabled,
            prefs.AdminEnabled,
            prefs.SystemEnabled);
    }

    private async Task<Domain.AppNotification> RequireOwnedAsync(
        int id,
        int userId,
        CancellationToken ct)
    {
        var item = await _store.GetAsync(id, ct)
            ?? throw new NotFoundException(
                "Notification not found.",
                "notification_not_found");
        if (item.UserId != userId || item.IsArchived)
        {
            throw new ForbiddenException("This notification is not yours.");
        }

        return item;
    }

    private static NotificationDto Map(Domain.AppNotification x) => new(
        x.Id,
        x.Title,
        x.Message,
        x.Type,
        NotificationTypes.CategoryOf(x.Type),
        x.IsRead,
        x.CreatedAt,
        x.ReadAt,
        x.GroupId,
        x.RelatedEntity,
        x.RelatedId,
        x.Link,
        x.Priority);
}
