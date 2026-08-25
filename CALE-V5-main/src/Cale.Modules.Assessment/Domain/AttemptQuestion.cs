namespace Cale.Modules.Assessment.Domain;

public sealed class AttemptQuestion
{
    public int Id { get; private set; }
    public int AttemptId { get; private set; }
    public int QuestionId { get; private set; }
    public int Order { get; private set; }

    private AttemptQuestion()
    {
    }

    public static AttemptQuestion Create(int attemptId, int questionId, int order) =>
        new()
        {
            AttemptId = attemptId,
            QuestionId = questionId,
            Order = order
        };
}
