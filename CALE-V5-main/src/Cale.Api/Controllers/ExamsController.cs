using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Catalog.Application;
using Cale.Modules.Catalog.Application.Commands;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Application.Queries;
using Cale.Modules.Classroom.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exams")]
public sealed class ExamsController : ControllerBase
{
    private readonly ListExamsHandler _list;
    private readonly SaveExamHandler _save;
    private readonly ImportExamFromWordHandler _importWord;
    private readonly ExportExamToWordHandler _exportWord;
    private readonly AssignExamToGroupHandler _assign;
    private readonly IClassroomStore _classroom;
    private readonly ITrainingEligibilityService _trainingEligibility;
    private readonly IClock _clock;

    public ExamsController(
        ListExamsHandler list,
        SaveExamHandler save,
        ImportExamFromWordHandler importWord,
        ExportExamToWordHandler exportWord,
        AssignExamToGroupHandler assign,
        IClassroomStore classroom,
        ITrainingEligibilityService trainingEligibility,
        IClock clock)
    {
        _list = list;
        _save = save;
        _importWord = importWord;
        _exportWord = exportWord;
        _assign = assign;
        _classroom = classroom;
        _trainingEligibility = trainingEligibility;
        _clock = clock;
    }

    [HttpGet]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        int? owner = CurrentUser.IsAdmin(User) ? null : CurrentUser.GetId(User);
        return Ok(await _list.HandleAsync(owner, ct));
    }

    [HttpGet("published")]
    public async Task<IActionResult> Published(CancellationToken ct)
    {
        var role = CurrentUser.GetRole(User);
        if (role is Roles.Admin or Roles.Teacher)
        {
            return Ok(await _list.PublishedAsync(ct));
        }

        var memberships = await _classroom.ListMembershipsAsync(
            CurrentUser.GetId(User),
            ct);
        var groupIds = memberships.Select(x => x.GroupId).ToList();
        var officialTheoryExamId = await _trainingEligibility.GetSchoolOfficialTheoryExamIdAsync(
            CurrentUser.GetId(User),
            ct);
        return Ok(await _list.PublishedForStudentAsync(
            groupIds,
            _clock.UtcNow,
            officialTheoryExamId,
            ct));
    }

    [HttpPost]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Create(
        SaveExamRequest request,
        CancellationToken ct) =>
        Ok(await _save.CreateAsync(request, CurrentUser.GetId(User), ct));

    [HttpGet("import/template")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public IActionResult ImportTemplate()
    {
        var bytes = ExamWordImportParser.BuildTemplateDocx();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "cale-plantilla-examen.docx");
    }

    [HttpPost("import")]
    [Authorize(Policy = "TeacherOrAdmin")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Import(
        IFormFile file,
        [FromForm] string? title,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Falta el archivo Word.",
                Detail = "missing_file",
                Status = 400
            });
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".docx")
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Solo se admiten archivos .docx",
                Detail = "invalid_exam_format",
                Status = 400
            });
        }

        await using var stream = file.OpenReadStream();
        var name = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(file.FileName)
            : title;
        var result = await _importWord.HandleAsync(stream, name, CurrentUser.GetId(User), ct);
        return Ok(result);
    }

    [HttpGet("{id:int}/export")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Export(int id, CancellationToken ct)
    {
        var (bytes, fileName) = await _exportWord.HandleAsync(
            id,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Update(
        int id,
        SaveExamRequest request,
        CancellationToken ct) =>
        Ok(await _save.UpdateAsync(
            id,
            request,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct));

    [HttpPost("{id:int}/publish")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Publish(
        int id,
        [FromQuery] bool published = true,
        CancellationToken ct = default)
    {
        await _save.PublishAsync(
            id,
            published,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct);
        return NoContent();
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Assign(
        int id,
        AssignExamToGroupRequest request,
        CancellationToken ct)
    {
        await _assign.HandleAsync(
            id,
            request,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct);
        return NoContent();
    }
}
