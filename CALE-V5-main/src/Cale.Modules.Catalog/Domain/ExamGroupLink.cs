namespace Cale.Modules.Catalog.Domain;

public sealed class ExamGroupLink
{
    public int Id { get; private set; }
    public int ExamId { get; private set; }
    public int GroupId { get; private set; }
    public DateTime? StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ExamGroupLink()
    {
    }

    public static ExamGroupLink Create(
        int examId,
        int groupId,
        DateTime? startsAt,
        DateTime? endsAt,
        DateTime utcNow)
    {
        return new ExamGroupLink
        {
            ExamId = examId,
            GroupId = groupId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedAt = utcNow
        };
    }
}
