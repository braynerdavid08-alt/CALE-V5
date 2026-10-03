using System.Text.Json;
using Cale.Api.Services.Courses;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Application;
using Cale.Modules.Courses.Domain;
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

    private CourseActor Teacher => new(9999, Roles.Teacher, _school.Id);

    private CourseActor OtherTeacher => new(9998, Roles.Teacher, _otherSchool.Id);

    private static JsonElement Blocks(params object[] blocks) => JsonSerializer.SerializeToElement(blocks);

    private static Dictionary<int, JsonElement> Answers(params (int Block, object Value)[] answers) =>
        answers.ToDictionary(a => a.Block, a => JsonSerializer.SerializeToElement(a.Value));

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
        var course = await _service.CreateAsync(Teacher, new CourseSaveRequest("Curso escuela", null, null, null, false), default);
        var lessonId = course.Lessons[0].Id;
        await _service.UpdateLessonAsync(Teacher, lessonId, new LessonSaveRequest("Lección", null, 5, Blocks(blocks)), default);
        await _service.UpdateAsync(Teacher, course.Id, new CourseSaveRequest("Curso escuela", null, null, null, true), default);
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
        var shown = CourseContent.ForStudent(json);
        int Shown(int block, string option) =>
            shown[block]!["options"]!.AsArray().Select(o => o!.GetValue<string>()).ToList().IndexOf(option);
        Assert.Equal(50, CourseContent.Score(json, Answers((1, Shown(1, "B")), (2, Shown(2, "A")))));
        Assert.Equal(Shown(2, "C"), CourseContent.Check(json, 2, 0).CorrectIndex);
        Assert.Equal(100, CourseContent.Score(CourseContent.Normalize(Blocks(new { type = "tip", body = "x" })), null));
    }

    [Fact]
    public void New_activities_hide_answers_and_are_graded_on_the_server()
    {
        var json = CourseContent.Normalize(Blocks(
            new { type = "truefalse", statement = "El PARE exige detenerse", answer = true, explanation = "Siempre." },
            new { type = "order", steps = new[] { "Uno", "Dos", "Tres", "Cuatro" } },
            new { type = "fillblank", text = "En zona escolar el límite es [[30]] km/h y en ciudad [[50|cincuenta]].", distractors = new[] { "60" } },
            new
            {
                type = "classify",
                groups = new[] { "Reglamentaria", "Preventiva" },
                items = new object[] { new { text = "Pare", group = 0 }, new { text = "Curva", group = 1 }, new { text = "No pase", group = 0 } }
            },
            new
            {
                type = "scenario",
                situation = "Un peatón cruza",
                choices = new object[] { new { text = "Pito", outcome = "Mal", best = false }, new { text = "Freno", outcome = "Bien", best = true } }
            },
            new { type = "hotspot", imageUrl = "/x.png", spots = new object[] { new { x = 10, y = 20, label = "Espejo" } } }));

        var student = CourseContent.ForStudent(json);
        var text = student.ToJsonString();
        Assert.DoesNotContain("\"answer\"", text);
        Assert.DoesNotContain("\"group\"", text);
        Assert.DoesNotContain("\"best\"", text);
        Assert.DoesNotContain("[[", text);
        Assert.Equal(5, CourseContent.QuizCount(json));

        var shownSteps = student[1]!["steps"]!.AsArray().Select(s => s!.GetValue<string>()).ToArray();
        Assert.NotEqual(new[] { "Uno", "Dos", "Tres", "Cuatro" }, shownSteps);
        Assert.Contains("60", student[2]!["bank"]!.AsArray().Select(s => s!.GetValue<string>()));

        var shownItems = student[3]!["items"]!.AsArray().Select(i => i!["text"]!.GetValue<string>()).ToList();
        var groups = shownItems.Select(t => t == "Curva" ? 1 : 0).ToArray();
        var shownChoices = student[4]!["choices"]!.AsArray().Select(c => c!["text"]!.GetValue<string>()).ToList();
        var brake = shownChoices.IndexOf("Freno");
        var honk = shownChoices.IndexOf("Pito");

        var all = Answers(
            (0, 0),
            (1, new[] { "uno", "Dos ", "TRES", "Cuatro" }),
            (2, new[] { "30", "Cincuenta" }),
            (3, groups),
            (4, brake));
        Assert.Equal(100, CourseContent.Score(json, all));

        var wrongOrder = CourseContent.Check(json, 1, 0, JsonSerializer.SerializeToElement(new[] { "Dos", "Uno", "Tres", "Cuatro" }));
        Assert.False(wrongOrder.Correct);
        Assert.Equal("Uno", wrongOrder.Solution![0]!.GetValue<string>());

        var badChoice = CourseContent.Check(json, 4, honk);
        Assert.False(badChoice.Correct);
        Assert.Equal(brake, badChoice.CorrectIndex);
        Assert.Equal("Mal", badChoice.Explanation);
        Assert.Equal("Bien", badChoice.Solution![brake]!.GetValue<string>());

        Assert.Throws<DomainException>(() => CourseContent.Check(json, 5, 0));
    }

    [Fact]
    public void Quiz_options_are_shuffled_except_positional_answers()
    {
        var quizzes = Enumerable.Range(0, 12)
            .Select(i => (object)new { type = "quiz", question = $"Pregunta {i}", options = new[] { "Mal", "Bien", "Peor", "Nunca" }, correct = 1 })
            .Append(new { type = "quiz", question = "¿Cuáles?", options = new[] { "Uno", "Dos", "Todas las anteriores" }, correct = 2 })
            .ToArray();
        var json = CourseContent.Normalize(Blocks(quizzes));
        var student = CourseContent.ForStudent(json);

        var positions = Enumerable.Range(0, 12)
            .Select(i => student[i]!["options"]!.AsArray().Select(o => o!.GetValue<string>()).ToList().IndexOf("Bien"))
            .ToList();
        Assert.True(positions.Distinct().Count() > 1);
        Assert.All(Enumerable.Range(0, 12), i => Assert.True(CourseContent.Check(json, i, positions[i]).Correct));

        Assert.Equal("Todas las anteriores", student[12]!["options"]![2]!.GetValue<string>());
        Assert.True(CourseContent.Check(json, 12, 2).Correct);
    }

    [Fact]
    public async Task Cannot_publish_course_without_content()
    {
        var course = await _service.CreateAsync(Teacher, new CourseSaveRequest("Vacío", null, null, null, false), default);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.UpdateAsync(Teacher, course.Id, new CourseSaveRequest("Vacío", null, null, null, true), default));
        Assert.Equal("course_empty", ex.ErrorCode);
    }

    [Fact]
    public async Task Teacher_cannot_edit_platform_original_but_can_copy_and_edit_it()
    {
        var platform = await _service.CreateAsync(Admin, new CourseSaveRequest("Plataforma", null, null, null, false), default);

        Assert.Contains(await _service.ListManageAsync(Teacher, default), c => c.Id == platform.Id && !c.CanEdit);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.UpdateAsync(Teacher, platform.Id, new CourseSaveRequest("Hack", null, null, null, false), default));

        var copy = await _service.DuplicateAsync(Teacher, platform.Id, default);
        Assert.True(copy.CanEdit);
        Assert.False(copy.IsPlatform);
        Assert.Single(copy.Lessons);
        await _service.UpdateAsync(Teacher, copy.Id, new CourseSaveRequest("Ajustada", null, null, null, false), default);

        Assert.Equal("Plataforma", (await _service.GetManageAsync(Admin, platform.Id, default)).Title);
    }

    [Fact]
    public async Task School_and_teacher_without_school_cannot_manage_courses()
    {
        var school = new CourseActor(_school.Id, Roles.School, _school.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.ListManageAsync(school, default));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.CreateAsync(school, new CourseSaveRequest("Nuevo", null, null, null, false), default));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ListManageAsync(new CourseActor(9997, Roles.Teacher, null), default));
    }

    [Fact]
    public async Task School_course_is_invisible_to_other_school_and_its_students()
    {
        var (courseId, _) = await PublishedSchoolCourseAsync(new { type = "text", body = "Hola" });

        await Assert.ThrowsAsync<DomainException>(() => _service.GetManageAsync(OtherTeacher, courseId, default));
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

        var first = await _service.CompleteAsync(_student.Id, lessonId, new LessonCompleteRequest(Answers((1, 1))), default);
        Assert.Equal(100, first.Score);
        Assert.True(first.FirstCompletion);
        Assert.True(first.CourseCompleted);

        var second = await _service.CompleteAsync(_student.Id, lessonId, new LessonCompleteRequest(Answers((1, 0))), default);
        Assert.Equal(0, second.Score);
        Assert.Equal(100, second.BestScore);
        Assert.False(second.FirstCompletion);

        Assert.Equal((1, 1), await _service.CompletionCountsAsync(_student.Id, default));
        var report = await _service.ProgressReportAsync(Teacher, courseId, default);
        Assert.Equal(100, report.Students.Single(s => s.StudentUserId == _student.Id).Percent);
    }

    [Fact]
    public async Task Seed_creates_published_platform_courses_once()
    {
        var seed = new CourseSeed(_db);
        await seed.EnsureAsync(null);
        await seed.EnsureAsync(null);

        var courses = await _service.ListForStudentAsync(_student.Id, default);
        Assert.Equal(4, courses.Count);
        Assert.Equal(5, Assert.Single(courses, c => c.Title == "Señalización vial e infraestructura").TotalLessons);
        Assert.Equal(6, Assert.Single(courses, c => c.Title == "Primeros auxilios en la vía").TotalLessons);
        var signs = Assert.Single(courses, c => c.Title == "Señales de tránsito");
        Assert.True(signs.TotalLessons >= 4);
        var rules = Assert.Single(courses, c => c.Title == "Normas de tránsito básicas");
        Assert.Equal(9, rules.TotalLessons);
    }

    [Fact]
    public async Task Seed_upgrades_untouched_platform_courses_and_respects_edits()
    {
        var seed = new CourseSeed(_db);
        await seed.EnsureAsync(null);
        var course = await _db.Set<Course>().SingleAsync(c => c.Slug == CourseSeed.RulesSlug);
        var lessons = await _db.Set<CourseLesson>().Where(l => l.CourseId == course.Id).OrderBy(l => l.Position).ToListAsync();
        var first = lessons[0];

        // An older seed version: different content in the first lesson, one lesson fewer, same stamps.
        _db.Entry(first).Property(l => l.ContentJson).CurrentValue = "[]";
        _db.Set<CourseLesson>().Remove(lessons[^1]);
        _db.Set<CourseLessonProgress>().Add(CourseLessonProgress.Create(course.Id, first.Id, _student.Id, 100, DateTime.UtcNow));
        await _db.SaveChangesAsync();

        await seed.EnsureAsync(null);
        var upgraded = await _db.Set<CourseLesson>().Where(l => l.CourseId == course.Id).OrderBy(l => l.Position).ToListAsync();
        Assert.Equal(9, upgraded.Count);
        Assert.Equal(first.Id, upgraded[0].Id);
        Assert.NotEqual("[]", upgraded[0].ContentJson);
        Assert.True(await _db.Set<CourseLessonProgress>().AnyAsync(p => p.LessonId == first.Id));

        // Once an editor touches the course, the seed leaves it alone.
        _db.Entry(upgraded[0]).Property(l => l.ContentJson).CurrentValue = "[]";
        course.Touch(DateTime.UtcNow.AddMinutes(5));
        await _db.SaveChangesAsync();
        await seed.EnsureAsync(null);
        Assert.Equal("[]", (await _db.Set<CourseLesson>().SingleAsync(l => l.Id == first.Id)).ContentJson);
    }
}
