namespace Cale.BuildingBlocks.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>From address shown to recipients.</summary>
    public string From { get; set; } = "noreply@micale.app";

    public string FromName { get; set; } = "Mi CALE";

    /// <summary>When false or SMTP host empty, SMTP is not used.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When SMTP is unavailable, skip email verification and activate the account immediately.
    /// Keep true only for local dev; production should configure SMTP and leave this false.
    /// </summary>
    public bool AutoConfirmWhenUnavailable { get; set; } = true;

    public SmtpOptions Smtp { get; set; } = new();

    public int CodeLength { get; set; } = 6;

    public int CodeExpiresMinutes { get; set; } = 15;
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; } = true;
}
