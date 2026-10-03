namespace Cale.Modules.Courses.Domain;

/// <summary>One row per student and lesson; keeps the best score of the completed attempts.</summary>
public sealed class CourseLessonProgress
{
    public int Id { get; private set; }
    public int CourseId { get; private set; }
    public int LessonId { get; private set; }
    public int StudentUserId { get; private set; }
    public int Score { get; private set; }
    public int Attempts { get; private set; }
    public DateTime CompletedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CourseLessonProgress()
    {
    }

    public static CourseLessonProgress Create(int courseId, int lessonId, int studentUserId, int score, DateTime utcNow) =>
        new()
        {
            CourseId = courseId,
            LessonId = lessonId,
            StudentUserId = studentUserId,
            Score = Math.Clamp(score, 0, 100),
            Attempts = 1,
            CompletedAt = utcNow,
            UpdatedAt = utcNow
        };

    public void RecordAttempt(int score, DateTime utcNow)
    {
        Attempts++;
        Score = Math.Max(Score, Math.Clamp(score, 0, 100));
        UpdatedAt = utcNow;
    }
}
