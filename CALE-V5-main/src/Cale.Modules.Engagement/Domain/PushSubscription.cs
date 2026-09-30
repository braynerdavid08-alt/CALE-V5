namespace Cale.Modules.Engagement.Domain;

/// <summary>A browser/device registered to receive Web Push notifications for a user.</summary>
public sealed class PushSubscription
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string P256dh { get; private set; } = string.Empty;
    public string Auth { get; private set; } = string.Empty;
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }

    private PushSubscription()
    {
    }

    public static PushSubscription Create(
        int userId,
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent,
        DateTime now) => new()
    {
        UserId = userId,
        Endpoint = endpoint,
        P256dh = p256dh,
        Auth = auth,
        UserAgent = userAgent,
        CreatedAt = now,
        LastSeenAt = now
    };

    public void Refresh(int userId, string p256dh, string auth, string? userAgent, DateTime now)
    {
        UserId = userId;
        P256dh = p256dh;
        Auth = auth;
        UserAgent = userAgent;
        LastSeenAt = now;
    }
}

/// <summary>Singleton row (Id = 1) holding the server VAPID key pair.</summary>
public sealed class PushVapidKeys
{
    public int Id { get; private set; }
    public string PublicKey { get; private set; } = string.Empty;
    public string PrivateKey { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private PushVapidKeys()
    {
    }

    public static PushVapidKeys Create(string publicKey, string privateKey, DateTime now) => new()
    {
        Id = 1,
        PublicKey = publicKey,
        PrivateKey = privateKey,
        CreatedAt = now
    };
}
