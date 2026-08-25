using Cale.Api.Extensions;
using Cale.Modules.Catalog.Application.Commands;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Application.Queries;
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
    private readonly AssignExamToGroupHandler _assign;

    public ExamsController(
        ListExamsHandler list,
        SaveExamHandler save,
        AssignExamToGroupHandler assign)
    {
        _list = list;
        _save = save;
        _assign = assign;
    }

    [HttpGet]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        int? owner = CurrentUser.IsAdmin(User) ? null : CurrentUser.GetId(User);
        return Ok(await _list.HandleAsync(owner, ct));
    }

    [HttpGet("published")]
    public async Task<IActionResult> Published(CancellationToken ct) =>
        Ok(await _list.PublishedAsync(ct));

    [HttpPost]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Create(
        SaveExamRequest request,
        CancellationToken ct) =>
        Ok(await _save.CreateAsync(request, CurrentUser.GetId(User), ct));

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
