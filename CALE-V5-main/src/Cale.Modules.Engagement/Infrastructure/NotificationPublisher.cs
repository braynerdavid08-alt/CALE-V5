using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Engagement.Application;
using Cale.Modules.Engagement.Application.Abstractions;
using Cale.Modules.Engagement.Domain;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.Engagement.Infrastructure;

public sealed class NotificationPublisher : INotificationPublisher, INotificationQueries
{
    private readonly INotificationStore _store;
    private readonly IClock _clock;
    private readonly ILogger<NotificationPublisher> _logger;

    public NotificationPublisher(
        INotificationStore store,
        IClock clock,
        ILogger<NotificationPublisher> logger)
    {
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    public Task NotifyUserAsync(
        int userId,
        string title,
        string message,
        string type,
        int? groupId,
        string? relatedEntity,
        int? relatedId,
        CancellationToken ct) =>
        NotifyUsersAsync(
            [userId],
            new NotificationDraft(
                title,
                message,
                type,
                groupId,
                relatedEntity,
                relatedId),
            ct);

    public Task NotifyUsersAsync(
        IReadOnlyList<int> userIds,
        string title,
        string message,
        string type,
        int? groupId,
        string? relatedEntity,
        int? relatedId,
        CancellationToken ct) =>
        NotifyUsersAsync(
            userIds,
            new NotificationDraft(
                title,
                message,
                type,
                groupId,
                relatedEntity,
                relatedId),
            ct);

    public async Task NotifyUsersAsync(
        IReadOnlyList<int> userIds,
        NotificationDraft draft,
        CancellationToken ct)
    {
        try
        {
            var distinct = userIds.Where(id => id > 0).Distinct().ToList();
            if (distinct.Count == 0
                || string.IsNullOrWhiteSpace(draft.Title)
                || string.IsNullOrWhiteSpace(draft.Message)
                || string.IsNullOrWhiteSpace(draft.Type))
            {
                return;
            }

            var category = NotificationTypes.CategoryOf(draft.Type);
            var allowed = new List<int>(distinct.Count);
            foreach (var userId in distinct)
            {
                if (await AllowsAsync(userId, category, ct))
                {
                    allowed.Add(userId);
                }
            }

            if (!string.IsNullOrWhiteSpace(draft.DedupeKey))
            {
                var already = await _store.FindUsersWithDedupeAsync(
                    allowed,
                    draft.DedupeKey!,
                    ct);
                allowed = allowed.Where(id => !already.Contains(id)).ToList();
            }

            if (allowed.Count == 0)
            {
                return;
            }

            var link = NotificationLinkResolver.Resolve(
                draft.Type,
                draft.GroupId,
                draft.RelatedEntity,
                draft.RelatedId,
                draft.Link);
            var now = _clock.UtcNow;
            var items = allowed.Select(id => AppNotification.Create(
                id,
                draft.Title,
                draft.Message,
                draft.Type,
                draft.GroupId,
                draft.RelatedEntity,
                draft.RelatedId,
                now,
                link,
                draft.Priority,
                draft.DedupeKey)).ToList();

            await _store.AddRangeAsync(items, ct);
            await _store.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Notifications published type={Type} count={Count} groupId={GroupId} dedupe={Dedupe}",
                draft.Type,
                items.Count,
                draft.GroupId,
                draft.DedupeKey);
        }
        catch (Exception ex)
        {
            // Never break the primary business action.
            _logger.LogError(
                ex,
                "Failed publishing notifications type={Type}",
                draft.Type);
        }
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct) =>
        _store.CountUnreadAsync(userId, ct);

    private async Task<bool> AllowsAsync(
        int userId,
        string category,
        CancellationToken ct)
    {
        var prefs = await _store.GetPreferenceAsync(userId, ct);
        if (prefs is null)
        {
            return true;
        }

        return prefs.AllowsCategory(category);
    }
}
