using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Scoring;

namespace Cale.Modules.Assessment.Domain;

public sealed class Attempt
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public int BankId { get; private set; }
    public int? ExamId { get; private set; }
    public string Mode { get; private set; } = "practice";
    public int TotalQuestions { get; private set; }
    public int CorrectCount { get; private set; }
    public decimal Percent { get; private set; }
    public bool Passed { get; private set; }
    public int TimeSeconds { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    private Attempt()
    {
    }

    public static Attempt Start(
        int userId,
        int bankId,
        int? examId,
        string mode,
        int totalQuestions,
        int timeMinutes,
        DateTime utcNow)
    {
        if (totalQuestions < 1)
        {
            throw new DomainException(
                "An attempt needs at least one question.",
                400,
                "invalid_count");
        }

        return new Attempt
        {
            UserId = userId,
            BankId = bankId,
            ExamId = examId,
            Mode = mode,
            TotalQuestions = totalQuestions,
            StartedAt = utcNow,
            TimeSeconds = timeMinutes * 60,
            ExpiresAt = utcNow.AddMinutes(timeMinutes)
        };
    }

    public bool IsOpen(DateTime utcNow) =>
        FinishedAt is null && (ExpiresAt is null || utcNow <= ExpiresAt);

    public void EnsureOwned(int userId)
    {
        if (UserId != userId)
        {
            throw new ForbiddenException("This attempt is not yours.");
        }
    }

    public void Finish(int correctCount, DateTime utcNow)
    {
        if (FinishedAt is not null)
        {
            throw new ConflictException(
                "Attempt already finished.",
                "attempt_finished");
        }

        if (ExpiresAt is { } expires && utcNow > expires.AddSeconds(5))
        {
            utcNow = expires;
        }

        CorrectCount = correctCount;
        Percent = TotalQuestions == 0
            ? 0
            : Math.Round(100m * correctCount / TotalQuestions, 2);
        Passed = ScoringRules.IsPassed(Percent);
        TimeSeconds = (int)Math.Max(0, (utcNow - StartedAt).TotalSeconds);
        FinishedAt = utcNow;
    }
}
