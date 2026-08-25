namespace Cale.BuildingBlocks.Domain.Scoring;

public static class ScoringRules
{
    public const decimal PassPercent = 80m;

    public static bool IsPassed(decimal percent) => percent >= PassPercent;
}
