namespace Cale.Modules.Assessment.Domain;

public sealed class AttemptAnswer
{
    public int Id { get; private set; }
    public int AttemptId { get; private set; }
    public int QuestionId { get; private set; }
    public int? OptionId { get; private set; }
    public bool IsCorrect { get; private set; }
    public string? QuestionTextSnapshot { get; private set; }
    public string? SelectedOptionSnapshot { get; private set; }
    public string? CorrectOptionSnapshot { get; private set; }
    public string? QuestionTypeSnapshot { get; private set; }

    private AttemptAnswer()
    {
    }

    public static AttemptAnswer Create(
        int attemptId,
        int questionId,
        int? optionId,
        bool isCorrect,
        string? questionText,
        string? selectedText,
        string? correctText,
        string? type)
    {
        return new AttemptAnswer
        {
            AttemptId = attemptId,
            QuestionId = questionId,
            OptionId = optionId,
            IsCorrect = isCorrect,
            QuestionTextSnapshot = questionText,
            SelectedOptionSnapshot = selectedText,
            CorrectOptionSnapshot = correctText,
            QuestionTypeSnapshot = type
        };
    }

    public void Update(
        int? optionId,
        bool isCorrect,
        string? selectedText,
        string? correctText)
    {
        OptionId = optionId;
        IsCorrect = isCorrect;
        SelectedOptionSnapshot = selectedText;
        CorrectOptionSnapshot = correctText;
    }
}
