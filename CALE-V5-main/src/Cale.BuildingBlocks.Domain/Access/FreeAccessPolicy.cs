namespace Cale.BuildingBlocks.Domain.Access;

/// <summary>
/// Pilot override (config <c>Access:FreeForAll</c>, default false).
/// When enabled, schools operate without a paid membership and seat caps are lifted.
/// Product model: students practice free; schools pay; instructors access via their school.
/// Keep plan/membership data so production can require membership without a data migration.
/// </summary>
public static class FreeAccessPolicy
{
    public const string ConfigKey = "Access:FreeForAll";

    public static bool Enabled { get; set; } = false;
}
