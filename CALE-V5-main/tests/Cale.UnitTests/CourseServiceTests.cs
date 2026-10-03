using System.Text.Json;
using Cale.Api.Services.Courses;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Application;
using Cale.Modules.Courses.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cale.UnitTests;

public sealed class CourseServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly CaleDbContext _db;
    private readonly CourseService _service;
    private readonly User _admin;
    private readonly User _school;
    private readonly User _otherSchool;
    private readonly User _student;
    private readonly User _otherStudent;

    public CourseServiceTests()
    {
        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, CoursesModelKey>()
            .Options;
        _db = new CaleDbContext(
            options,
            new MappingAssemblies(typeof(UserConfiguration).Assembly, typeof(CourseConfiguration).Assembly));
        _db.Database.EnsureCreated();

        _admin = User.CreateAdmin("Admin", "admin@test.co", "x", Now);
        _school = User.RegisterSchool("Escuela A", "a@test.co", "x", Now);
        _otherSchool = User.RegisterSchool("Escuela B", "b@test.co", "x", Now);
        _db.AddRange(_admin, _school, _otherSchool);
        _db.SaveChanges();
        _student = User.RegisterStudent("Ana", "ana@test.co", "x", Now, _school.Id);
        _otherStudent = User.RegisterStudent("Beto", "beto@test.co", "x", Now, _otherSchool.Id);
        _db.AddRange(_student, _otherStudent);
        _db.SaveChanges();

        _service = new CourseService(_db, new UserStore(_db));
    }

    public void Dispose() => _db.Dispose();

    /// <summary>EF caches one model per context type; other fixtures map different assemblies on the same type.</summary>
    private sealed class CoursesModelKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => (context.GetType(), nameof(CoursesModelKey), designTime);
    }

    private CourseActor Admin => new(_admin.Id, Roles.Admin, null);

    private CourseActor School => new(_school.Id, Roles.School, _school.Id);

    private CourseActor OtherSchool => new(_otherSchool.Id, Roles.School, _otherSchool.Id);

    private static JsonElement Blocks(params object[] blocks) => JsonSerializer.SerializeToElement(blocks);

    private static object QuizBlock(int correct = 1) => new
    {
        type = "quiz",
        question = "¿Qué significa?",
        options = new[] { "A", "B", "C" },
        correct,
        explanation = "Porque sí"
    };

    private async Task<(int CourseId, int LessonId)> PublishedSchoolCourseAsync(params object[] blocks)
    {
        var course = await _service.CreateAsync(School, new CourseSaveRequest("Curso escuela", null, null, null, false), default);
        var lessonId = course.Lessons[0].Id;
        await _service.UpdateLessonAsync(School, lessonId, new LessonSaveRequest("Lección", null, 5, Blocks(blocks)), default);
        await _service.UpdateAsync(School, course.Id, new CourseSaveRequest("Curso escuela", null, null, null, true), default);
        return (course.Id, lessonId);
    }

    [Fact]
    public void Normalize_rejects_unknown_block_and_bad_quiz()
    {
        Assert.Throws<DomainException>(() => CourseContent.Normalize(Blocks(new { type = "html", body = "<b>x</b>" })));
        Assert.Throws<DomainException>(() => CourseContent.Normalize(Blocks(QuizBlock(correct: 7))));
        Assert.Throws<DomainException>(() => CourseContent.Normalize(Blocks(new { type = "image", url = "javascript:alert(1)" })));
    }

    [Fact]
    public void ForStudent_hides_answers_and_Score_counts_quizzes()
    {
        var json = CourseContent.Normalize(Blocks(new { type = "text", body = "Hola" }, QuizBlock(1), QuizBlock(2)));

        var student = CourseContent.ForStudent(json).ToJsonString();
        Assert.DoesNotContain("correct", student);
        Assert.DoesNotContain("Porque", student);
        Assert.Equal(2, CourseContent.QuizCount(json));
        Assert.Equal(50, CourseContent.Score(json, new Dictionary<int, int> { [1] = 1, [2] = 0 }));
        Assert.Equal(100, CourseContent.Score(CourseContent.Normalize(Blocks(new { type = "tip", body = "x" })), null));
    }

    [Fact]
    public async Task Cannot_publish_course_without_content()
    {
        var course = await _service.CreateAsync(School, new CourseSaveRequest("Vacío", null, null, null, false), default);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.UpdateAsync(School, course.Id, new CourseSaveRequest("Vacío", null, null, null, true), default));
        Assert.Equal("course_empty", ex.ErrorCode);
    }

    [Fact]
    public async Task School_cannot_edit_platform_course_but_can_duplicate_it()
    {
        var platform = await _service.CreateAsync(Admin, new CourseSaveRequest("Plataforma", null, null, null, false), default);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.UpdateAsync(School, platform.Id, new CourseSaveRequest("Hack", null, null, null, false), default));

        var copy = await _service.DuplicateAsync(School, platform.Id, default);
        Assert.True(copy.CanEdit);
        Assert.False(copy.IsPlatform);
        Assert.Single(copy.Lessons);
    }

    [Fact]
    public async Task School_course_is_invisible_to_other_school_and_its_students()
    {
        var (courseId, _) = await PublishedSchoolCourseAsync(new { type = "text", body = "Hola" });

        await Assert.ThrowsAsync<DomainException>(() => _service.GetManageAsync(OtherSchool, courseId, default));
        Assert.Empty(await _service.ListForStudentAsync(_otherStudent.Id, default));
        Assert.Single(await _service.ListForStudentAsync(_student.Id, default));
    }

    [Fact]
    public async Task Student_lesson_hides_answers_and_completion_keeps_best_score()
    {
        var (courseId, lessonId) = await PublishedSchoolCourseAsync(new { type = "text", body = "Hola" }, QuizBlock(1));

        var lesson = await _service.GetLessonForStudentAsync(_student.Id, lessonId, default);
        Assert.DoesNotContain("correct", lesson.Content.ToJsonString());

        var check = await _service.CheckAsync(_student.Id, lessonId, new QuizCheckRequest(1, 0), default);
        Assert.False(check.Correct);
        Assert.Equal(1, check.CorrectIndex);

        var first = await _service.CompleteAsync(_student.Id, lessonId, new LessonCompleteRequest(new Dictionary<int, int> { [1] = 1 }), default);
        Assert.Equal(100, first.Score);
        Assert.True(first.FirstCompletion);
        Assert.True(first.CourseCompleted);

        var second = await _service.CompleteAsync(_student.Id, lessonId, new LessonCompleteRequest(new Dictionary<int, int> { [1] = 0 }), default);
        Assert.Equal(0, second.Score);
        Assert.Equal(100, second.BestScore);
        Assert.False(second.FirstCompletion);

        Assert.Equal((1, 1), await _service.CompletionCountsAsync(_student.Id, default));
        var report = await _service.ProgressReportAsync(School, courseId, default);
        Assert.Equal(100, report.Students.Single(s => s.StudentUserId == _student.Id).Percent);
    }

    [Fact]
    public async Task Seed_creates_published_signs_course_once()
    {
        var seed = new CourseSeed(_db);
        await seed.EnsureAsync(null);
        await seed.EnsureAsync(null);

        var courses = await _service.ListForStudentAsync(_student.Id, default);
        var signs = Assert.Single(courses);
        Assert.Equal("Señales de tránsito", signs.Title);
        Assert.True(signs.TotalLessons >= 4);
    }
}
