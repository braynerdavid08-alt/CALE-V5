namespace Cale.BuildingBlocks.Domain.Assessment;

public static class AttemptModes
{
    public const string Practice = "practice";
    public const string MixedPractice = "mixed_practice";
    public const string Exam = "exam";

    /// <summary>Simulacro with the official CALE structure and the 80 % / 80 % pass rule.</summary>
    public const string Official = "official";
}
