namespace Cale.Modules.Catalog.Domain;

public sealed class ExamQuestion
{
    public int Id { get; private set; }
    public int ExamId { get; private set; }
    public int QuestionId { get; private set; }
    public int Order { get; private set; }

    private ExamQuestion()
    {
    }
}
