namespace Cale.BuildingBlocks.Domain.Scoring;

public static class ScoringRules
{
    public const int MaxIncorrectAnswers = 3;

    /// <summary>Failed attempts with at most this many wrong answers are shown as "close to passing".</summary>
    public const int MaxIncorrectForNearPass = MaxIncorrectAnswers * 2;

    public const string BandPassed = "passed";
    public const string BandNear = "near";
    public const string BandFailed = "failed";

    public static bool IsPassed(int correctCount, int totalQuestions)
    {
        if (totalQuestions <= 0 || correctCount < 0 || correctCount > totalQuestions)
        {
            return false;
        }

        return totalQuestions - correctCount <= MaxIncorrectAnswers;
    }

    /// <summary>Minimum percent needed to pass an attempt with this many questions.</summary>
    public static decimal PassThresholdPercent(int totalQuestions)
    {
        if (totalQuestions <= 0)
        {
            return 100m;
        }

        var minCorrect = Math.Max(0, totalQuestions - MaxIncorrectAnswers);
        return Math.Round(100m * minCorrect / totalQuestions, 1);
    }

    /// <summary>Official CALE theory exam (Res. 20253040037125 de 2025): 80 % in knowledge and 80 % in attitudes.</summary>
    public const decimal OfficialPassPercent = 80m;

    public static bool IsOfficialPassed(
        int knowledgeCorrect,
        int knowledgeTotal,
        int attitudeCorrect,
        int attitudeTotal) =>
        ReachesOfficialPercent(knowledgeCorrect, knowledgeTotal)
        && ReachesOfficialPercent(attitudeCorrect, attitudeTotal);

    private static bool ReachesOfficialPercent(int correct, int total) =>
        total > 0
        && correct >= 0
        && correct <= total
        && 100m * correct / total >= OfficialPassPercent;

    public static string ResultBand(int correctCount, int totalQuestions)
    {
        if (IsPassed(correctCount, totalQuestions))
        {
            return BandPassed;
        }

        return totalQuestions > 0
            && correctCount >= 0
            && totalQuestions - correctCount <= MaxIncorrectForNearPass
            ? BandNear
            : BandFailed;
    }
}
