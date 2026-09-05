using Cale.BuildingBlocks.Domain.Catalog;
using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Catalog.Domain;

public sealed class Question
{
    private readonly List<QuestionOption> _options = [];

    public int Id { get; private set; }
    public int? CreatedById { get; private set; }
    public int BankId { get; private set; }
    public int BlockId { get; private set; }
    public string Text { get; private set; } = "";
    public string Type { get; private set; } = QuestionTypes.MultipleChoice;
    public string? Subject { get; private set; }
    public string? Topic { get; private set; }
    public string? Subtopic { get; private set; }
    public string? Difficulty { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? Source { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? Explanation { get; private set; }
    public string? WhyIncorrect { get; private set; }
    public string? Hint { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<QuestionOption> Options => _options;

    private Question()
    {
    }

    public static Question Create(
        int bankId,
        int blockId,
        int? createdById,
        string text,
        string type,
        string? topic,
        string? imageUrl,
        string? explanation,
        IReadOnlyList<QuestionOption> options,
        DateTime utcNow)
    {
        Validate(text, type, options);
        var question = new Question
        {
            BankId = bankId,
            BlockId = blockId,
            CreatedById = createdById,
            Text = text.Trim(),
            Type = type.Trim(),
            Topic = topic?.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
            Explanation = explanation?.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            IsActive = true
        };
        question._options.AddRange(options);
        return question;
    }

    public void SetCatalogMeta(
        string? subject,
        string? topic,
        string? subtopic,
        string? difficulty,
        string? source)
    {
        Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
        Topic = string.IsNullOrWhiteSpace(topic) ? Topic : topic.Trim();
        Subtopic = string.IsNullOrWhiteSpace(subtopic) ? null : subtopic.Trim();
        Difficulty = string.IsNullOrWhiteSpace(difficulty) ? null : difficulty.Trim();
        Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
    }

    public void Replace(
        int bankId,
        int blockId,
        string text,
        string type,
        string? topic,
        string? imageUrl,
        string? explanation,
        IReadOnlyList<QuestionOption> options,
        DateTime utcNow)
    {
        Validate(text, type, options);
        BankId = bankId;
        BlockId = blockId;
        Text = text.Trim();
        Type = type.Trim();
        Topic = topic?.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        Explanation = explanation?.Trim();
        UpdatedAt = utcNow;
        _options.Clear();
        _options.AddRange(options);
    }

    public void SetActive(bool active) => IsActive = active;

    public bool CanEdit(int userId, bool isAdmin) =>
        isAdmin || (CreatedById is int owner && owner == userId);

    private static void Validate(
        string text,
        string type,
        IReadOnlyList<QuestionOption> options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Question text is required.", 400, "invalid_text");
        }

        if (!QuestionTypes.IsValid(type) && type != QuestionTypes.MultipleChoice)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new DomainException("Question type is required.", 400, "invalid_type");
            }
        }

        if (options.Count < 2)
        {
            throw new DomainException(
                "A question needs at least two options.",
                400,
                "invalid_options");
        }

        if (options.Count(x => x.IsCorrect) != 1)
        {
            throw new DomainException(
                "Exactly one option must be marked as correct.",
                400,
                "invalid_correct");
        }

        if (options.Any(x =>
            string.IsNullOrWhiteSpace(x.Text) && string.IsNullOrWhiteSpace(x.ImageUrl)))
        {
            throw new DomainException(
                "Each option needs text or an image.",
                400,
                "invalid_options");
        }
    }
}
