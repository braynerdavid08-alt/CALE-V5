namespace Cale.BuildingBlocks.Domain.Classroom;

public static class ActivityTypes
{
    public const string Activity = "actividad";
    public const string Workshop = "taller";
    public const string Assignment = "trabajo";

    public static bool IsValid(string? type) =>
        type is Activity or Workshop or Assignment;
}
