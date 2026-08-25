namespace Cale.BuildingBlocks.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "Cale.Api";
    public string Audience { get; set; } = "Cale.Frontend";
    public int ExpirationHours { get; set; } = 12;
}
