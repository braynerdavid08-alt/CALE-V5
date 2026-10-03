using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Courses.Domain;

/// <summary>
/// Self-paced course. <see cref="SchoolUserId"/> null means a platform course (managed by admins,
/// visible to every school); otherwise it belongs to that school and only its students see it.
/// </summary>
public sealed class Course
{
    public int Id { get; private set; }
    public int? SchoolUserId { get; private set; }
    public int OwnerUserId { get; private set; }
    /// <summary>Stable key for seeded platform courses, so seeding never duplicates them.</summary>
    public string? Slug { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public string Category { get; private set; } = CourseCategories.Default;
    public string? CoverUrl { get; private set; }
    public bool IsPublished { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Course()
    {
    }

    public static Course Create(
        int? schoolUserId,
        int ownerUserId,
        string title,
        string? description,
        string? category,
        string? coverUrl,
        DateTime utcNow,
        string? slug = null)
    {
        var course = new Course
        {
            SchoolUserId = schoolUserId,
            OwnerUserId = ownerUserId,
            Slug = slug,
            CreatedAt = utcNow
        };
        course.Update(title, description, category, coverUrl, false, utcNow);
        return course;
    }

    public void Update(
        string title,
        string? description,
        string? category,
        string? coverUrl,
        bool isPublished,
        DateTime utcNow)
    {
        Title = CourseLimits.RequiredText(title, CourseLimits.TitleMax, "El curso necesita un título.");
        Description = CourseLimits.OptionalText(description, CourseLimits.DescriptionMax);
        Category = CourseCategories.Normalize(category);
        CoverUrl = CourseLimits.OptionalText(coverUrl, CourseLimits.UrlMax);
        IsPublished = isPublished;
        UpdatedAt = utcNow;
    }

    public void Touch(DateTime utcNow) => UpdatedAt = utcNow;

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        IsPublished = false;
        UpdatedAt = utcNow;
    }
}

public static class CourseCategories
{
    public const string Default = "Formación vial";

    public static readonly string[] All =
    [
        "Señales de tránsito",
        "Normas de tránsito",
        "Vehículo seguro",
        "Motociclistas",
        "Peatones y ciclistas",
        "Primeros auxilios",
        Default
    ];

    public static string Normalize(string? value)
    {
        var v = (value ?? "").Trim();
        return All.FirstOrDefault(c => c.Equals(v, StringComparison.OrdinalIgnoreCase)) ?? Default;
    }
}

public static class CourseLimits
{
    public const int TitleMax = 160;
    public const int DescriptionMax = 1000;
    public const int SummaryMax = 500;
    public const int UrlMax = 500;
    public const int MaxLessonsPerCourse = 40;

    public static string RequiredText(string? value, int max, string message)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0)
        {
            throw new DomainException(message, 400, "course_field_required");
        }

        return v.Length > max ? v[..max] : v;
    }

    public static string? OptionalText(string? value, int max)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0)
        {
            return null;
        }

        return v.Length > max ? v[..max] : v;
    }
}
