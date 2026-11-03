namespace Cale.BuildingBlocks.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>From address shown to recipients (SMTP). Hostinger From is the mailbox itself.</summary>
    public string From { get; set; } = "notificaciones@cealuzverde.com";

    public string FromName { get; set; } = "Luz Verde";

    /// <summary>
    /// When false, registration may auto-confirm (local/dev). When true, a real provider
    /// must be configured; misconfiguration must not auto-confirm.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>HostingerMail | Smtp | Auto</summary>
    public string Provider { get; set; } = EmailProviders.Auto;

    public SmtpOptions Smtp { get; set; } = new();

    public HostingerMailOptions HostingerMail { get; set; } = new();

    public int CodeLength { get; set; } = 6;

    public int CodeExpiresMinutes { get; set; } = 15;
}

public static class EmailProviders
{
    public const string HostingerMail = "HostingerMail";
    public const string Smtp = "Smtp";
    public const string Auto = "Auto";
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; } = true;
}

public sealed class HostingerMailOptions
{
    /// <summary>Bearer token from Agentic Mail → API access (never commit real values).</summary>
    public string ApiToken { get; set; } = "";

    /// <summary>Mailbox resource id from GET /api/v1/me (e.g. ACxxxx).</summary>
    public string MailboxResourceId { get; set; } = "";

    public string BaseUrl { get; set; } = "https://api.mail.hostinger.com";

    public int TimeoutSeconds { get; set; } = 15;

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ApiToken)
        && !string.IsNullOrWhiteSpace(MailboxResourceId)
        && !string.IsNullOrWhiteSpace(BaseUrl);
}
