namespace Cale.Modules.Catalog.Domain;

public sealed class QuestionOption
{
    public int Id { get; private set; }
    public int QuestionId { get; private set; }
    public string Text { get; private set; } = "";
    public bool IsCorrect { get; private set; }
    public string? ImageUrl { get; private set; }

    private QuestionOption()
    {
    }

    public static QuestionOption Create(string text, bool isCorrect, string? imageUrl)
    {
        return new QuestionOption
        {
            Text = text.Trim(),
            IsCorrect = isCorrect,
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim()
        };
    }
}
