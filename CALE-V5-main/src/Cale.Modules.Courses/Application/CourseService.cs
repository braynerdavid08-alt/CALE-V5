using System.Text.Json.Nodes;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Domain;
using Cale.Modules.Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Courses.Application;

public sealed class CourseService
{
    private readonly CaleDbContext _db;
    private readonly IUserStore _users;

    public CourseService(CaleDbContext db, IUserStore users)
    {
        _db = db;
        _users = users;
    }

    private DbSet<Course> Courses => _db.Set<Course>();
    private DbSet<CourseLesson> Lessons => _db.Set<CourseLesson>();
    private DbSet<CourseLessonProgress> Progress => _db.Set<CourseLessonProgress>();

    // ── Editor ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CourseManageItemDto>> ListManageAsync(CourseActor actor, CancellationToken ct)
    {
        EnsureManager(actor);
        var query = Courses.AsNoTracking().Where(c => c.IsActive);
        query = actor.Role == Roles.Admin
            ? query.Where(c => c.SchoolUserId == null)
            : query.Where(c => c.SchoolUserId == null || c.SchoolUserId == actor.SchoolUserId);
        var courses = await query.OrderBy(c => c.SchoolUserId == null).ThenByDescending(c => c.UpdatedAt).ToListAsync(ct);
        var counts = await LessonCountsAsync(courses.Select(c => c.Id).ToList(), ct);
        return courses
            .Select(c => new CourseManageItemDto(
                c.Id,
                c.Title,
                c.Description,
                c.Category,
                c.CoverUrl,
                c.IsPublished,
                c.SchoolUserId is null,
                CanEdit(actor, c),
                counts.GetValueOrDefault(c.Id),
                c.UpdatedAt))
            .ToList();
    }

    public async Task<CourseManageDetailDto> GetManageAsync(CourseActor actor, int courseId, CancellationToken ct)
    {
        EnsureManager(actor);
        var course = await VisibleToManagerAsync(actor, courseId, ct);
        var lessons = await Lessons.AsNoTracking()
            .Where(l => l.CourseId == courseId)
            .OrderBy(l => l.Position).ThenBy(l => l.Id)
            .ToListAsync(ct);
        return new CourseManageDetailDto(
            course.Id,
            course.Title,
            course.Description,
            course.Category,
            course.CoverUrl,
            course.IsPublished,
            course.SchoolUserId is null,
            CanEdit(actor, course),
            lessons.Select(ToManageDto).ToList(),
            CourseCategories.All);
    }

    public async Task<CourseManageDetailDto> CreateAsync(CourseActor actor, CourseSaveRequest request, CancellationToken ct)
    {
        EnsureManager(actor);
        var now = DateTime.UtcNow;
        var course = Course.Create(
            actor.Role == Roles.Admin ? null : actor.SchoolUserId,
            actor.UserId,
            request.Title,
            request.Description,
            request.Category,
            request.CoverUrl,
            now);
        Courses.Add(course);
        await _db.SaveChangesAsync(ct);
        Lessons.Add(CourseLesson.Create(course.Id, 0, "Lección 1", now));
        await _db.SaveChangesAsync(ct);
        return await GetManageAsync(actor, course.Id, ct);
    }

    public async Task UpdateAsync(CourseActor actor, int courseId, CourseSaveRequest request, CancellationToken ct)
    {
        var course = await EditableAsync(actor, courseId, ct);
        if (request.IsPublished && !await Lessons.AnyAsync(l => l.CourseId == courseId && l.ContentJson != "[]", ct))
        {
            throw new DomainException("Agrega contenido a por lo menos una lección antes de publicar.", 400, "course_empty");
        }

        course.Update(request.Title, request.Description, request.Category, request.CoverUrl, request.IsPublished, DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CourseActor actor, int courseId, CancellationToken ct)
    {
        var course = await EditableAsync(actor, courseId, ct);
        course.Deactivate(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Copies a course (usually a platform one) into the school so it can be customized.</summary>
    public async Task<CourseManageDetailDto> DuplicateAsync(CourseActor actor, int courseId, CancellationToken ct)
    {
        EnsureManager(actor);
        var source = await VisibleToManagerAsync(actor, courseId, ct);
        var now = DateTime.UtcNow;
        var copy = Course.Create(
            actor.Role == Roles.Admin ? null : actor.SchoolUserId,
            actor.UserId,
            $"{source.Title} (copia)",
            source.Description,
            source.Category,
            source.CoverUrl,
            now);
        Courses.Add(copy);
        await _db.SaveChangesAsync(ct);
        var lessons = await Lessons.AsNoTracking().Where(l => l.CourseId == courseId).ToListAsync(ct);
        Lessons.AddRange(lessons.Select(l => l.CloneTo(copy.Id, now)));
        await _db.SaveChangesAsync(ct);
        return await GetManageAsync(actor, copy.Id, ct);
    }

    public async Task<LessonManageDto> AddLessonAsync(CourseActor actor, int courseId, LessonCreateRequest request, CancellationToken ct)
    {
        var course = await EditableAsync(actor, courseId, ct);
        var count = await Lessons.CountAsync(l => l.CourseId == courseId, ct);
        if (count >= CourseLimits.MaxLessonsPerCourse)
        {
            throw new DomainException($"Un curso puede tener máximo {CourseLimits.MaxLessonsPerCourse} lecciones.", 400, "course_lessons_limit");
        }

        var now = DateTime.UtcNow;
        var title = string.IsNullOrWhiteSpace(request.Title) ? $"Lección {count + 1}" : request.Title;
        var lesson = CourseLesson.Create(courseId, count, title, now);
        Lessons.Add(lesson);
        course.Touch(now);
        await _db.SaveChangesAsync(ct);
        return ToManageDto(lesson);
    }

    public async Task<LessonManageDto> UpdateLessonAsync(CourseActor actor, int lessonId, LessonSaveRequest request, CancellationToken ct)
    {
        var lesson = await Lessons.FirstOrDefaultAsync(l => l.Id == lessonId, ct) ?? throw LessonNotFound();
        var course = await EditableAsync(actor, lesson.CourseId, ct);
        var now = DateTime.UtcNow;
        lesson.Update(request.Title, request.Summary, request.EstimatedMinutes, CourseContent.Normalize(request.Content), now);
        course.Touch(now);
        await _db.SaveChangesAsync(ct);
        return ToManageDto(lesson);
    }

    public async Task DeleteLessonAsync(CourseActor actor, int lessonId, CancellationToken ct)
    {
        var lesson = await Lessons.FirstOrDefaultAsync(l => l.Id == lessonId, ct) ?? throw LessonNotFound();
        var course = await EditableAsync(actor, lesson.CourseId, ct);
        var now = DateTime.UtcNow;
        Lessons.Remove(lesson);
        Progress.RemoveRange(await Progress.Where(p => p.LessonId == lessonId).ToListAsync(ct));
        var rest = await Lessons.Where(l => l.CourseId == course.Id && l.Id != lessonId)
            .OrderBy(l => l.Position).ThenBy(l => l.Id)
            .ToListAsync(ct);
        for (var i = 0; i < rest.Count; i++)
        {
            rest[i].MoveTo(i, now);
        }

        if (rest.Count == 0)
        {
            course.Update(course.Title, course.Description, course.Category, course.CoverUrl, false, now);
        }

        course.Touch(now);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReorderLessonsAsync(CourseActor actor, int courseId, LessonReorderRequest request, CancellationToken ct)
    {
        var course = await EditableAsync(actor, courseId, ct);
        var lessons = await Lessons.Where(l => l.CourseId == courseId).ToListAsync(ct);
        var order = (request.LessonIds ?? []).Distinct().ToList();
        if (order.Count != lessons.Count || lessons.Any(l => !order.Contains(l.Id)))
        {
            throw new DomainException("El orden enviado no coincide con las lecciones del curso.", 400, "course_reorder_invalid");
        }

        var now = DateTime.UtcNow;
        foreach (var lesson in lessons)
        {
            lesson.MoveTo(order.IndexOf(lesson.Id), now);
        }

        course.Touch(now);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CourseProgressReportDto> ProgressReportAsync(CourseActor actor, int courseId, CancellationToken ct)
    {
        EnsureManager(actor);
        var course = await VisibleToManagerAsync(actor, courseId, ct);
        var total = await Lessons.CountAsync(l => l.CourseId == courseId, ct);
        var rows = await Progress.AsNoTracking().Where(p => p.CourseId == courseId).ToListAsync(ct);

        List<(int Id, string Name)> students;
        if (actor.Role is Roles.School or Roles.Teacher && actor.SchoolUserId is int schoolId)
        {
            students = (await _users.ListBySchoolAsync(schoolId, ct))
                .Where(u => u.IsActive && Roles.Normalize(u.Role) == Roles.Student)
                .Select(u => (u.Id, u.Name))
                .ToList();
        }
        else
        {
            var ids = rows.Select(r => r.StudentUserId).Distinct().ToList();
            var names = await _users.GetNamesAsync(ids, ct);
            students = ids.Select(id => (id, names.GetValueOrDefault(id) ?? $"Estudiante {id}")).ToList();
        }

        var byStudent = rows.GroupBy(r => r.StudentUserId).ToDictionary(g => g.Key, g => g.ToList());
        var report = students
            .Select(s =>
            {
                var mine = byStudent.GetValueOrDefault(s.Id) ?? [];
                return new CourseStudentProgressDto(
                    s.Id,
                    s.Name,
                    mine.Count,
                    total,
                    Percent(mine.Count, total),
                    mine.Count == 0 ? 0 : (int)Math.Round(mine.Average(r => r.Score)),
                    mine.Count == 0 ? null : mine.Max(r => r.UpdatedAt));
            })
            .OrderByDescending(s => s.Percent)
            .ThenBy(s => s.StudentName)
            .ToList();
        return new CourseProgressReportDto(course.Id, course.Title, total, report);
    }

    // ── Student ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<StudentCourseItemDto>> ListForStudentAsync(int studentUserId, CancellationToken ct)
    {
        var schoolId = await StudentSchoolAsync(studentUserId, ct);
        var courses = await StudentCourses(schoolId)
            .OrderBy(c => c.SchoolUserId == null)
            .ThenBy(c => c.Title)
            .ToListAsync(ct);
        var ids = courses.Select(c => c.Id).ToList();
        var lessons = await Lessons.AsNoTracking()
            .Where(l => ids.Contains(l.CourseId))
            .Select(l => new { l.Id, l.CourseId, l.Position })
            .ToListAsync(ct);
        var done = (await Progress.AsNoTracking()
                .Where(p => p.StudentUserId == studentUserId && ids.Contains(p.CourseId))
                .Select(p => p.LessonId)
                .ToListAsync(ct))
            .ToHashSet();

        return courses
            .Select(c =>
            {
                var mine = lessons.Where(l => l.CourseId == c.Id).OrderBy(l => l.Position).ThenBy(l => l.Id).ToList();
                var completed = mine.Count(l => done.Contains(l.Id));
                var next = mine.FirstOrDefault(l => !done.Contains(l.Id))?.Id;
                return new StudentCourseItemDto(
                    c.Id,
                    c.Title,
                    c.Description,
                    c.Category,
                    c.CoverUrl,
                    c.SchoolUserId is null,
                    mine.Count,
                    completed,
                    Percent(completed, mine.Count),
                    next);
            })
            .ToList();
    }

    public async Task<StudentCourseDetailDto> GetForStudentAsync(int studentUserId, int courseId, CancellationToken ct)
    {
        var course = await StudentCourseAsync(studentUserId, courseId, ct);
        var lessons = await Lessons.AsNoTracking()
            .Where(l => l.CourseId == courseId)
            .OrderBy(l => l.Position).ThenBy(l => l.Id)
            .ToListAsync(ct);
        var progress = await Progress.AsNoTracking()
            .Where(p => p.StudentUserId == studentUserId && p.CourseId == courseId)
            .ToDictionaryAsync(p => p.LessonId, ct);
        var completed = lessons.Count(l => progress.ContainsKey(l.Id));
        return new StudentCourseDetailDto(
            course.Id,
            course.Title,
            course.Description,
            course.Category,
            course.CoverUrl,
            lessons.Count,
            completed,
            Percent(completed, lessons.Count),
            lessons.Select((l, i) => new StudentLessonItemDto(
                    l.Id,
                    i + 1,
                    l.Title,
                    l.Summary,
                    l.EstimatedMinutes,
                    progress.ContainsKey(l.Id),
                    progress.TryGetValue(l.Id, out var p) ? p.Score : null))
                .ToList());
    }

    public async Task<StudentLessonDto> GetLessonForStudentAsync(int studentUserId, int lessonId, CancellationToken ct)
    {
        var lesson = await Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId, ct) ?? throw LessonNotFound();
        var course = await StudentCourseAsync(studentUserId, lesson.CourseId, ct);
        var order = await Lessons.AsNoTracking()
            .Where(l => l.CourseId == course.Id)
            .OrderBy(l => l.Position).ThenBy(l => l.Id)
            .Select(l => l.Id)
            .ToListAsync(ct);
        var index = order.IndexOf(lessonId);
        var progress = await Progress.AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentUserId == studentUserId && p.LessonId == lessonId, ct);
        return new StudentLessonDto(
            lesson.Id,
            course.Id,
            course.Title,
            index + 1,
            order.Count,
            lesson.Title,
            lesson.Summary,
            lesson.EstimatedMinutes,
            CourseContent.ForStudent(lesson.ContentJson),
            CourseContent.QuizCount(lesson.ContentJson),
            progress is not null,
            progress?.Score,
            index > 0 ? order[index - 1] : null,
            index >= 0 && index < order.Count - 1 ? order[index + 1] : null);
    }

    public async Task<QuizCheckResult> CheckAsync(int studentUserId, int lessonId, QuizCheckRequest request, CancellationToken ct)
    {
        var lesson = await Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId, ct) ?? throw LessonNotFound();
        await StudentCourseAsync(studentUserId, lesson.CourseId, ct);
        var (correct, correctIndex, explanation, solution) =
            CourseContent.Check(lesson.ContentJson, request.BlockIndex, request.Option, request.Answer);
        return new QuizCheckResult(correct, correctIndex, explanation, solution);
    }

    public async Task<LessonCompleteResult> CompleteAsync(int studentUserId, int lessonId, LessonCompleteRequest request, CancellationToken ct)
    {
        var lesson = await Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId, ct) ?? throw LessonNotFound();
        var course = await StudentCourseAsync(studentUserId, lesson.CourseId, ct);
        var score = CourseContent.Score(lesson.ContentJson, request.Answers);
        var now = DateTime.UtcNow;

        var row = await Progress.FirstOrDefaultAsync(p => p.StudentUserId == studentUserId && p.LessonId == lessonId, ct);
        var first = row is null;
        if (row is null)
        {
            row = CourseLessonProgress.Create(course.Id, lessonId, studentUserId, score, now);
            Progress.Add(row);
        }
        else
        {
            row.RecordAttempt(score, now);
        }

        await _db.SaveChangesAsync(ct);

        var order = await Lessons.AsNoTracking()
            .Where(l => l.CourseId == course.Id)
            .OrderBy(l => l.Position).ThenBy(l => l.Id)
            .Select(l => l.Id)
            .ToListAsync(ct);
        var done = (await Progress.AsNoTracking()
                .Where(p => p.StudentUserId == studentUserId && p.CourseId == course.Id)
                .Select(p => p.LessonId)
                .ToListAsync(ct))
            .ToHashSet();
        var completed = order.Count(done.Contains);
        var index = order.IndexOf(lessonId);
        var next = order.Skip(index + 1).Concat(order.Take(index + 1)).FirstOrDefault(id => !done.Contains(id));
        return new LessonCompleteResult(
            score,
            row.Score,
            completed,
            order.Count,
            completed == order.Count && order.Count > 0,
            first,
            next == 0 ? null : next);
    }

    /// <summary>Lessons and fully completed courses, for XP and badges.</summary>
    public Task<(int Lessons, int Courses)> CompletionCountsAsync(int studentUserId, CancellationToken ct) =>
        CompletionCountsAsync(_db, studentUserId, ct);

    public static async Task<(int Lessons, int Courses)> CompletionCountsAsync(
        CaleDbContext db,
        int studentUserId,
        CancellationToken ct)
    {
        var rows = await db.Set<CourseLessonProgress>().AsNoTracking()
            .Where(p => p.StudentUserId == studentUserId)
            .Select(p => new { p.CourseId, p.LessonId })
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return (0, 0);
        }

        var courseIds = rows.Select(r => r.CourseId).Distinct().ToList();
        var totals = await db.Set<CourseLesson>().AsNoTracking()
            .Where(l => courseIds.Contains(l.CourseId))
            .GroupBy(l => l.CourseId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var lessonIds = (await db.Set<CourseLesson>().AsNoTracking()
                .Where(l => courseIds.Contains(l.CourseId))
                .Select(l => l.Id)
                .ToListAsync(ct))
            .ToHashSet();
        var valid = rows.Where(r => lessonIds.Contains(r.LessonId)).ToList();
        var coursesDone = valid
            .GroupBy(r => r.CourseId)
            .Count(g => totals.GetValueOrDefault(g.Key) > 0 && g.Count() >= totals[g.Key]);
        return (valid.Count, coursesDone);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static void EnsureManager(CourseActor actor)
    {
        if (actor.Role == Roles.Admin)
        {
            return;
        }

        if (actor.Role is Roles.School or Roles.Teacher && actor.SchoolUserId is not null)
        {
            return;
        }

        throw new ForbiddenException("Solo la escuela, sus instructores o el administrador pueden gestionar cursos.", "course_forbidden");
    }

    private static bool CanEdit(CourseActor actor, Course course) =>
        actor.Role == Roles.Admin
            ? course.SchoolUserId is null
            : course.SchoolUserId is not null && course.SchoolUserId == actor.SchoolUserId;

    private async Task<Course> VisibleToManagerAsync(CourseActor actor, int courseId, CancellationToken ct)
    {
        var course = await Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.IsActive, ct) ?? throw CourseNotFound();
        var visible = actor.Role == Roles.Admin
            ? course.SchoolUserId is null
            : course.SchoolUserId is null || course.SchoolUserId == actor.SchoolUserId;
        return visible ? course : throw CourseNotFound();
    }

    private async Task<Course> EditableAsync(CourseActor actor, int courseId, CancellationToken ct)
    {
        EnsureManager(actor);
        var course = await VisibleToManagerAsync(actor, courseId, ct);
        if (!CanEdit(actor, course))
        {
            throw new ForbiddenException(
                "Este curso es de Luz Verde. Haz una copia para tu escuela si quieres cambiarlo.",
                "course_read_only");
        }

        return course;
    }

    private IQueryable<Course> StudentCourses(int? schoolId) =>
        Courses.AsNoTracking().Where(c =>
            c.IsActive && c.IsPublished && (c.SchoolUserId == null || c.SchoolUserId == schoolId));

    private async Task<Course> StudentCourseAsync(int studentUserId, int courseId, CancellationToken ct)
    {
        var schoolId = await StudentSchoolAsync(studentUserId, ct);
        return await StudentCourses(schoolId).FirstOrDefaultAsync(c => c.Id == courseId, ct) ?? throw CourseNotFound();
    }

    private async Task<int?> StudentSchoolAsync(int studentUserId, CancellationToken ct) =>
        (await _users.GetByIdAsync(studentUserId, ct))?.SchoolId;

    private async Task<Dictionary<int, int>> LessonCountsAsync(List<int> courseIds, CancellationToken ct) =>
        await Lessons.AsNoTracking()
            .Where(l => courseIds.Contains(l.CourseId))
            .GroupBy(l => l.CourseId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    private static LessonManageDto ToManageDto(CourseLesson l) =>
        new(l.Id, l.Position, l.Title, l.Summary, l.EstimatedMinutes, CourseContent.Parse(l.ContentJson));

    private static int Percent(int done, int total) => total == 0 ? 0 : (int)Math.Round(done * 100.0 / total);

    private static DomainException CourseNotFound() => new("No encontramos ese curso.", 404, "course_not_found");

    private static DomainException LessonNotFound() => new("No encontramos esa lección.", 404, "course_lesson_not_found");
}
