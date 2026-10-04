namespace Cale.Modules.Courses.Domain;

public sealed class CourseLesson
{
    public int Id { get; private set; }
    public int CourseId { get; private set; }
    public int Position { get; private set; }
    public string Title { get; private set; } = "";
    public string? Summary { get; private set; }
    public int EstimatedMinutes { get; private set; } = 10;
    /// <summary>JSON array of content blocks; validated by <c>CourseContent</c>.</summary>
    public string ContentJson { get; private set; } = "[]";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    /// <summary>Stable key of a platform seed lesson; null for lessons created in the editor.</summary>
    public string? SeedKey { get; private set; }
    /// <summary>Fingerprint of what the seed last wrote; a different current fingerprint means someone edited the lesson.</summary>
    public string? SeedHash { get; private set; }

    private CourseLesson()
    {
    }

    public static CourseLesson Create(int courseId, int position, string title, DateTime utcNow)
    {
        var lesson = new CourseLesson
        {
            CourseId = courseId,
            Position = Math.Max(0, position),
            CreatedAt = utcNow
        };
        lesson.Update(title, null, 10, "[]", utcNow);
        return lesson;
    }

    public void Update(string title, string? summary, int estimatedMinutes, string contentJson, DateTime utcNow)
    {
        Title = CourseLimits.RequiredText(title, CourseLimits.TitleMax, "La lección necesita un título.");
        Summary = CourseLimits.OptionalText(summary, CourseLimits.SummaryMax);
        EstimatedMinutes = Math.Clamp(estimatedMinutes, 1, 240);
        ContentJson = contentJson;
        UpdatedAt = utcNow;
    }

    public void MoveTo(int position, DateTime utcNow)
    {
        Position = Math.Max(0, position);
        UpdatedAt = utcNow;
    }

    public void AssignSeedKey(string key) => SeedKey = key;

    public void MarkSeeded(string key, string hash)
    {
        SeedKey = key;
        SeedHash = hash;
    }

    public CourseLesson CloneTo(int courseId, DateTime utcNow)
    {
        var copy = Create(courseId, Position, Title, utcNow);
        copy.Update(Title, Summary, EstimatedMinutes, ContentJson, utcNow);
        return copy;
    }
}
