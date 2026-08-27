using Cale.Api.Extensions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "TeacherOrAdmin")]
[Route("api/teacher")]
public sealed class TeacherController : ControllerBase
{
    private readonly SchoolJoinRequestHandler _join;

    public TeacherController(SchoolJoinRequestHandler join) => _join = join;

    [HttpPost("school-join-requests")]
    public async Task<ActionResult<SchoolJoinRequestDto>> RequestJoin(
        [FromBody] RequestSchoolJoinRequest request,
        CancellationToken ct)
    {
        if (CurrentUser.IsAdmin(User))
        {
            return Forbid();
        }

        return Ok(await _join.RequestAsync(CurrentUser.GetId(User), request, ct));
    }

    [HttpGet("school-join-requests")]
    public async Task<ActionResult<IReadOnlyList<SchoolJoinRequestDto>>> MyJoinRequests(
        CancellationToken ct)
    {
        if (CurrentUser.IsAdmin(User))
        {
            return Ok(Array.Empty<SchoolJoinRequestDto>());
        }

        return Ok(await _join.ListMineAsync(CurrentUser.GetId(User), ct));
    }

    [HttpPost("school-join-requests/{id:int}/cancel")]
    public async Task<IActionResult> CancelJoin(int id, CancellationToken ct)
    {
        await _join.CancelAsync(CurrentUser.GetId(User), id, ct);
        return Ok(new { ok = true });
    }
}
