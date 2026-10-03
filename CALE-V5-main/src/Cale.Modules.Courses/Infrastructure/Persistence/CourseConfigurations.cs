using Cale.Modules.Courses.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Courses.Infrastructure.Persistence;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Cursos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(80);
        builder.Property(x => x.Title).HasMaxLength(CourseLimits.TitleMax).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(CourseLimits.DescriptionMax);
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.CoverUrl).HasMaxLength(CourseLimits.UrlMax);
        builder.HasIndex(x => x.SchoolUserId);
        builder.HasIndex(x => x.Slug);
    }
}

public sealed class CourseLessonConfiguration : IEntityTypeConfiguration<CourseLesson>
{
    public void Configure(EntityTypeBuilder<CourseLesson> builder)
    {
        builder.ToTable("CursoLecciones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(CourseLimits.TitleMax).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(CourseLimits.SummaryMax);
        builder.Property(x => x.ContentJson).IsRequired();
        builder.HasIndex(x => new { x.CourseId, x.Position });
    }
}

public sealed class CourseLessonProgressConfiguration : IEntityTypeConfiguration<CourseLessonProgress>
{
    public void Configure(EntityTypeBuilder<CourseLessonProgress> builder)
    {
        builder.ToTable("CursoProgresoLecciones");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.LessonId, x.StudentUserId }).IsUnique();
        builder.HasIndex(x => new { x.CourseId, x.StudentUserId });
        builder.HasIndex(x => x.StudentUserId);
    }
}
