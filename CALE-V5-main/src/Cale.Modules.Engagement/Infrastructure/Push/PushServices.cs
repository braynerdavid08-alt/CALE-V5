using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebPush;
using DeviceSubscription = Cale.Modules.Engagement.Domain.PushSubscription;

namespace Cale.Modules.Engagement.Infrastructure.Push;

public sealed record PushMessage(
    IReadOnlyList<int> UserIds,
    string Title,
    string Body,
    string? Url,
    string Tag);

/// <summary>In-memory queue so publishing a notification never waits on push delivery.</summary>
public sealed class PushQueue
{
    private readonly Channel<PushMessage> _channel = Channel.CreateBounded<PushMessage>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(PushMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<PushMessage> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Resolves the VAPID key pair: configuration (Push:VapidPublicKey / Push:VapidPrivateKey) first,
/// otherwise a pair generated once and stored in the database so every instance shares it.
/// </summary>
public sealed class PushKeyProvider
{
    private readonly IConfiguration _config;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private VapidDetails? _cached;

    public PushKeyProvider(IConfiguration config) => _config = config;

    public async Task<VapidDetails> GetAsync(CaleDbContext db, IClock clock, CancellationToken ct)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var subject = _config["Push:Subject"];
            if (string.IsNullOrWhiteSpace(subject))
            {
                subject = "mailto:notificaciones@micale.app";
            }

            var pub = _config["Push:VapidPublicKey"];
            var priv = _config["Push:VapidPrivateKey"];
            if (string.IsNullOrWhiteSpace(pub) || string.IsNullOrWhiteSpace(priv))
            {
                var row = await db.Set<PushVapidKeys>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
                if (row is null)
                {
                    var generated = VapidHelper.GenerateVapidKeys();
                    db.Set<PushVapidKeys>().Add(PushVapidKeys.Create(generated.PublicKey, generated.PrivateKey, clock.UtcNow));
                    try
                    {
                        await db.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateException)
                    {
                        db.ChangeTracker.Clear();
                    }

                    row = await db.Set<PushVapidKeys>().AsNoTracking().FirstAsync(x => x.Id == 1, ct);
                }

                pub = row.PublicKey;
                priv = row.PrivateKey;
            }

            _cached = new VapidDetails(subject, pub, priv);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>Registers and removes the device subscriptions of the current user.</summary>
public sealed class PushSubscriptionService
{
    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly PushKeyProvider _keys;
    private readonly PushQueue _queue;

    public PushSubscriptionService(CaleDbContext db, IClock clock, PushKeyProvider keys, PushQueue queue)
    {
        _db = db;
        _clock = clock;
        _keys = keys;
        _queue = queue;
    }

    public async Task<string> GetPublicKeyAsync(CancellationToken ct) =>
        (await _keys.GetAsync(_db, _clock, ct)).PublicKey;

    public async Task SubscribeAsync(
        int userId,
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent,
        CancellationToken ct)
    {
        var ua = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent;
        var now = _clock.UtcNow;
        var existing = await _db.Set<DeviceSubscription>().FirstOrDefaultAsync(x => x.Endpoint == endpoint, ct);
        if (existing is null)
        {
            _db.Set<DeviceSubscription>().Add(DeviceSubscription.Create(userId, endpoint, p256dh, auth, ua, now));
        }
        else if (existing.UserId == userId || (existing.P256dh == p256dh && existing.Auth == auth))
        {
            // Another user may only take over a device that proves it is the same browser subscription.
            existing.Refresh(userId, p256dh, auth, ua, now);
        }
        else
        {
            return;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task UnsubscribeAsync(int userId, string endpoint, CancellationToken ct)
    {
        var rows = await _db.Set<DeviceSubscription>()
            .Where(x => x.Endpoint == endpoint && x.UserId == userId)
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return;
        }

        _db.Set<DeviceSubscription>().RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> CountDevicesAsync(int userId, CancellationToken ct) =>
        await _db.Set<DeviceSubscription>().CountAsync(x => x.UserId == userId, ct);

    public void SendTest(int userId) =>
        _queue.Enqueue(new PushMessage(
            [userId],
            "¡Notificaciones activadas!",
            "Así te avisaremos cuando tu docente o tu escuela te escriban.",
            null,
            "cale-test"));
}

/// <summary>Delivers queued push messages and prunes subscriptions the push service reports as gone.</summary>
public sealed class PushDispatcher : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PushQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly PushKeyProvider _keys;
    private readonly ILogger<PushDispatcher> _logger;
    private readonly WebPushClient _client = new();

    public PushDispatcher(
        PushQueue queue,
        IServiceScopeFactory scopes,
        PushKeyProvider keys,
        ILogger<PushDispatcher> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _keys = keys;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await DeliverAsync(message, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Push delivery failed tag={Tag}", message.Tag);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task DeliverAsync(PushMessage message, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var userIds = message.UserIds.Distinct().ToList();
        var subs = await db.Set<DeviceSubscription>()
            .Where(x => userIds.Contains(x.UserId))
            .ToListAsync(ct);
        if (subs.Count == 0)
        {
            return;
        }

        var subUsers = subs.Select(s => s.UserId).Distinct().ToList();
        var unread = await db.Set<AppNotification>()
            .Where(n => subUsers.Contains(n.UserId) && !n.IsRead && !n.IsArchived)
            .GroupBy(n => n.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var vapid = await _keys.GetAsync(db, clock, ct);
        var gone = new List<DeviceSubscription>();
        var sent = 0;

        foreach (var sub in subs)
        {
            var payload = JsonSerializer.Serialize(new
            {
                title = message.Title,
                body = message.Body,
                url = string.IsNullOrWhiteSpace(message.Url) ? "/" : message.Url,
                tag = message.Tag,
                badge = unread.GetValueOrDefault(sub.UserId)
            }, Json);

            try
            {
                await _client.SendNotificationAsync(
                    new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth),
                    payload,
                    new Dictionary<string, object>
                    {
                        ["vapidDetails"] = vapid,
                        ["TTL"] = 86400
                    },
                    ct);
                sent++;
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                gone.Add(sub);
            }
            catch (WebPushException ex)
            {
                _logger.LogWarning("Push rejected status={Status} userId={UserId}", (int)ex.StatusCode, sub.UserId);
            }
        }

        if (gone.Count > 0)
        {
            db.Set<DeviceSubscription>().RemoveRange(gone);
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Push sent tag={Tag} devices={Sent} removed={Removed}",
            message.Tag,
            sent,
            gone.Count);
    }
}
