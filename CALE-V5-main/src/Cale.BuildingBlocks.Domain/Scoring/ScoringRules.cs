namespace Cale.BuildingBlocks.Domain.Scoring;

public static class ScoringRules
{
    public const int MaxIncorrectAnswers = 3;

    public static bool IsPassed(int correctCount, int totalQuestions)
    {
        if (totalQuestions <= 0 || correctCount < 0 || correctCount > totalQuestions)
        {
            return false;
        }

        return totalQuestions - correctCount <= MaxIncorrectAnswers;
    }
}
