using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Catalog.Domain;

public sealed class Exam
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public int? BankId { get; private set; }
    public int QuestionCount { get; private set; }
    public int TimeMinutes { get; private set; }
    public int AllowedAttempts { get; private set; }
    public bool Randomize { get; private set; }
    public bool Published { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int CreatedById { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }

    private Exam()
    {
    }

    public static Exam Create(
        string name,
        string? description,
        int? bankId,
        int questionCount,
        int timeMinutes,
        int allowedAttempts,
        bool randomize,
        int createdById,
        DateTime? startsAt,
        DateTime? endsAt,
        DateTime utcNow)
    {
        Validate(name, questionCount, timeMinutes, allowedAttempts);
        return new Exam
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            BankId = bankId,
            QuestionCount = questionCount,
            TimeMinutes = timeMinutes,
            AllowedAttempts = Math.Max(1, allowedAttempts),
            Randomize = randomize,
            Published = false,
            IsActive = true,
            CreatedById = createdById,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            StartsAt = startsAt,
            EndsAt = endsAt
        };
    }

    public void Update(
        string name,
        string? description,
        int? bankId,
        int questionCount,
        int timeMinutes,
        int allowedAttempts,
        bool randomize,
        DateTime? startsAt,
        DateTime? endsAt,
        DateTime utcNow)
    {
        Validate(name, questionCount, timeMinutes, allowedAttempts);
        Name = name.Trim();
        Description = description?.Trim();
        BankId = bankId;
        QuestionCount = questionCount;
        TimeMinutes = timeMinutes;
        AllowedAttempts = Math.Max(1, allowedAttempts);
        Randomize = randomize;
        StartsAt = startsAt;
        EndsAt = endsAt;
        UpdatedAt = utcNow;
    }

    public void SetPublished(bool published, DateTime utcNow)
    {
        Published = published;
        UpdatedAt = utcNow;
    }

    public void SetActive(bool active) => IsActive = active;

    public bool CanEdit(int userId, bool isAdmin) =>
        isAdmin || CreatedById == userId;

    public bool IsOpenAt(DateTime utcNow)
    {
        if (StartsAt is { } start && utcNow < start)
        {
            return false;
        }

        if (EndsAt is { } end && utcNow > end)
        {
            return false;
        }

        return Published && IsActive;
    }

    private static void Validate(
        string name,
        int questionCount,
        int timeMinutes,
        int allowedAttempts)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Exam name is required.", 400, "invalid_name");
        }

        if (questionCount < 1)
        {
            throw new DomainException(
                "Question count must be at least 1.",
                400,
                "invalid_count");
        }

        if (timeMinutes < 1)
        {
            throw new DomainException(
                "Time must be at least 1 minute.",
                400,
                "invalid_time");
        }

        if (allowedAttempts < 1)
        {
            throw new DomainException(
                "Allowed attempts must be at least 1.",
                400,
                "invalid_attempts");
        }
    }
}
