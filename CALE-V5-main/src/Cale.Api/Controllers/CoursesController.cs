using Cale.Api.Extensions;
using Cale.Api.Infrastructure;
using Cale.Api.Services.Play;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Courses.Application;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Presentation.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin + "," + Roles.School)]
[Route("api/courses")]
public sealed class CoursesController : ControllerBase
{
    private static readonly Dictionary<string, (string Kind, string ContentType)> MediaTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ("image", "image/jpeg"),
            [".jpeg"] = ("image", "image/jpeg"),
            [".png"] = ("image", "image/png"),
            [".gif"] = ("image", "image/gif"),
            [".webp"] = ("image", "image/webp"),
            [".svg"] = ("image", "image/svg+xml"),
            [".mp4"] = ("video", "video/mp4"),
            [".webm"] = ("video", "video/webm"),
            [".mov"] = ("video", "video/quicktime"),
            [".m4v"] = ("video", "video/x-m4v"),
            [".mp3"] = ("audio", "audio/mpeg"),
            [".m4a"] = ("audio", "audio/mp4"),
            [".ogg"] = ("audio", "audio/ogg"),
            [".wav"] = ("audio", "audio/wav")
        };

    private readonly CourseService _courses;
    private readonly IUserStore _users;
    private readonly IPresentationMediaStore _media;
    private readonly PlayService _play;

    public CoursesController(CourseService courses, IUserStore users, IPresentationMediaStore media, PlayService play)
    {
        _courses = courses;
        _users = users;
        _media = media;
        _play = play;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _courses.ListManageAsync(await ActorAsync(ct), ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(await _courses.GetManageAsync(await ActorAsync(ct), id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CourseSaveRequest request, CancellationToken ct) =>
        Ok(await _courses.CreateAsync(await ActorAsync(ct), request, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CourseSaveRequest request, CancellationToken ct)
    {
        await _courses.UpdateAsync(await ActorAsync(ct), id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _courses.DeleteAsync(await ActorAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/duplicate")]
    public async Task<IActionResult> Duplicate(int id, CancellationToken ct) =>
        Ok(await _courses.DuplicateAsync(await ActorAsync(ct), id, ct));

    [HttpPost("{id:int}/lessons")]
    public async Task<IActionResult> AddLesson(int id, LessonCreateRequest request, CancellationToken ct) =>
        Ok(await _courses.AddLessonAsync(await ActorAsync(ct), id, request, ct));

    [HttpPut("{id:int}/lessons/order")]
    public async Task<IActionResult> Reorder(int id, LessonReorderRequest request, CancellationToken ct)
    {
        await _courses.ReorderLessonsAsync(await ActorAsync(ct), id, request, ct);
        return NoContent();
    }

    [HttpPut("lessons/{lessonId:int}")]
    public async Task<IActionResult> UpdateLesson(int lessonId, LessonSaveRequest request, CancellationToken ct) =>
        Ok(await _courses.UpdateLessonAsync(await ActorAsync(ct), lessonId, request, ct));

    [HttpDelete("lessons/{lessonId:int}")]
    public async Task<IActionResult> DeleteLesson(int lessonId, CancellationToken ct)
    {
        await _courses.DeleteLessonAsync(await ActorAsync(ct), lessonId, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/progress")]
    public async Task<IActionResult> Progress(int id, CancellationToken ct) =>
        Ok(await _courses.ProgressReportAsync(await ActorAsync(ct), id, ct));

    [HttpGet("signs")]
    public IReadOnlyList<SignDto> Signs() => _play.GetSigns();

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimits.PresentationMediaBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.PresentationMediaBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Selecciona un archivo.", 400, "invalid_file");
        }

        if (file.Length > UploadLimits.PresentationMediaBytes)
        {
            throw new DomainException("El archivo debe pesar 100 MB o menos.", 400, "course_file_too_large");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!MediaTypes.TryGetValue(ext, out var type))
        {
            throw new DomainException(
                "Usa imagen (jpg, png, webp, svg), video (mp4, webm, mov) o audio (mp3, m4a, ogg, wav).",
                400,
                "invalid_file");
        }

        await using var stream = file.OpenReadStream();
        var url = await _media.SaveAsync(stream, $"{Guid.NewGuid():N}{ext}", type.ContentType, CurrentUser.GetId(User), ct);
        return Ok(new { url, mediaType = type.Kind });
    }

    private async Task<CourseActor> ActorAsync(CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var role = CurrentUser.GetRole(User);
        int? schoolUserId = null;
        if (role == Roles.School)
        {
            schoolUserId = userId;
        }
        else if (role != Roles.Admin)
        {
            schoolUserId = (await _users.GetByIdAsync(userId, ct))?.SchoolId;
        }

        return new CourseActor(userId, role, schoolUserId);
    }
}

[ApiController]
[Authorize(Policy = "StudentOnly")]
[Route("api/student/courses")]
public sealed class StudentCoursesController : ControllerBase
{
    private readonly CourseService _courses;
    private readonly PlayService _play;

    public StudentCoursesController(CourseService courses, PlayService play)
    {
        _courses = courses;
        _play = play;
    }

    private int UserId => CurrentUser.GetId(User);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _courses.ListForStudentAsync(UserId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(await _courses.GetForStudentAsync(UserId, id, ct));

    [HttpGet("lessons/{lessonId:int}")]
    public async Task<IActionResult> Lesson(int lessonId, CancellationToken ct) =>
        Ok(await _courses.GetLessonForStudentAsync(UserId, lessonId, ct));

    [HttpPost("lessons/{lessonId:int}/check")]
    public async Task<IActionResult> Check(int lessonId, QuizCheckRequest request, CancellationToken ct) =>
        Ok(await _courses.CheckAsync(UserId, lessonId, request, ct));

    [HttpPost("lessons/{lessonId:int}/complete")]
    public async Task<IActionResult> Complete(int lessonId, LessonCompleteRequest request, CancellationToken ct)
    {
        var result = await _courses.CompleteAsync(UserId, lessonId, request, ct);
        var newBadges = await _play.CheckAchievementsAsync(UserId, ct);
        return Ok(new { result, newBadges });
    }
}
