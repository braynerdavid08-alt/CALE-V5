using Cale.Api.Extensions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cale.Api.Controllers;

/// <summary>
/// Teacher/student side of school linking: request to join a school, or accept/reject an invitation.
/// The handler rejects any other role.
/// </summary>
[ApiController]
[Authorize]
[Route("api/me/school-membership")]
public sealed class SchoolMembershipController : ControllerBase
{
    private readonly SchoolJoinRequestHandler _join;

    public SchoolMembershipController(SchoolJoinRequestHandler join) => _join = join;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SchoolJoinRequestDto>>> Mine(CancellationToken ct) =>
        Ok(await _join.ListMineAsync(CurrentUser.GetId(User), ct));

    [HttpPost("requests")]
    [EnableRateLimiting(RateLimitPolicies.SchoolLinks)]
    public async Task<ActionResult<SchoolJoinRequestDto>> RequestJoin(
        [FromBody] RequestSchoolJoinRequest request,
        CancellationToken ct) =>
        Ok(await _join.RequestAsync(CurrentUser.GetId(User), request, ct));

    [HttpPost("requests/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _join.CancelAsync(CurrentUser.GetId(User), id, ct);
        return NoContent();
    }

    [HttpPost("invitations/{id:int}/accept")]
    public async Task<ActionResult<SchoolJoinRequestDto>> AcceptInvite(int id, CancellationToken ct) =>
        Ok(await _join.AcceptInviteAsync(CurrentUser.GetId(User), id, ct));

    [HttpPost("invitations/{id:int}/reject")]
    public async Task<ActionResult<SchoolJoinRequestDto>> RejectInvite(int id, CancellationToken ct) =>
        Ok(await _join.RejectInviteAsync(CurrentUser.GetId(User), id, ct));
}
