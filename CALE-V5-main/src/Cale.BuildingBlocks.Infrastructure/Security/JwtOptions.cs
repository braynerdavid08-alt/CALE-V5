namespace Cale.BuildingBlocks.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "Cale.Api";
    public string Audience { get; set; } = "Cale.Frontend";
    public int ExpirationHours { get; set; } = 12;
    public int AccessTokenMinutes { get; set; } = 60;
    /// <summary>Each refresh issues a new token with this lifetime, so the session only ends after this many days without using the app.</summary>
    public int RefreshTokenDays { get; set; } = 365;

    public int EffectiveRefreshTokenDays => RefreshTokenDays > 0 ? RefreshTokenDays : 365;
}
