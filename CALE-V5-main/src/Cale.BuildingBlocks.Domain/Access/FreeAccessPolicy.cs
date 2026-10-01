namespace Cale.BuildingBlocks.Domain.Access;

/// <summary>
/// Free-for-all mode (config <c>Access:FreeForAll</c>, default true): every registered
/// user gets full product access with or without a school, and seat caps are lifted.
/// Plan/membership data is kept so the paid model can be switched back on.
/// </summary>
public static class FreeAccessPolicy
{
    public const string ConfigKey = "Access:FreeForAll";

    public static bool Enabled { get; set; } = true;
}
