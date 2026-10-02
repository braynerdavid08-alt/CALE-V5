using Cale.Api.Extensions;
using Cale.Api.Services.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

/// <summary>Users propose CALE questions or ideas; only the admin decides.</summary>
[ApiController]
[Authorize]
[Route("api/requests")]
public sealed class UserRequestsController : ControllerBase
{
    private readonly UserRequestService _service;

    public UserRequestsController(UserRequestService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<UserRequestDto>> Create(CreateUserRequest request, CancellationToken ct) =>
        Ok(await _service.CreateAsync(CurrentUser.GetId(User), request, ct));

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<UserRequestDto>>> Mine(CancellationToken ct) =>
        Ok(await _service.ListMineAsync(CurrentUser.GetId(User), ct));

    [HttpGet("mine/status")]
    public async Task<ActionResult<MyRequestStatusDto>> MyStatus(CancellationToken ct) =>
        Ok(await _service.MyStatusAsync(CurrentUser.GetId(User), ct));

    [HttpGet("admin/blocked")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<IReadOnlyList<BlockedUserDto>>> Blocked(CancellationToken ct) =>
        Ok(await _service.ListBlockedAsync(ct));

    [HttpPost("admin/blocked/{userId:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Block(int userId, BlockUserRequest request, CancellationToken ct)
    {
        var rejected = await _service.BlockAsync(CurrentUser.GetId(User), userId, request, ct);
        return Ok(new { rejected });
    }

    [HttpDelete("admin/blocked/{userId:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Unblock(int userId, CancellationToken ct)
    {
        await _service.UnblockAsync(userId, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _service.CancelAsync(CurrentUser.GetId(User), id, ct);
        return NoContent();
    }

    [HttpGet("admin")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<IReadOnlyList<UserRequestDto>>> ListForAdmin(
        [FromQuery] string? status,
        [FromQuery] string? kind,
        CancellationToken ct) =>
        Ok(await _service.ListForAdminAsync(status, kind, ct));

    [HttpGet("admin/counts")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<UserRequestCountsDto>> Counts(CancellationToken ct) =>
        Ok(await _service.CountsAsync(ct));

    [HttpPost("{id:int}/accept")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<UserRequestDto>> Accept(int id, AcceptUserRequest request, CancellationToken ct) =>
        Ok(await _service.AcceptAsync(CurrentUser.GetId(User), id, request, ct));

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<UserRequestDto>> Reject(int id, RejectUserRequest request, CancellationToken ct) =>
        Ok(await _service.RejectAsync(CurrentUser.GetId(User), id, request, ct));
}
