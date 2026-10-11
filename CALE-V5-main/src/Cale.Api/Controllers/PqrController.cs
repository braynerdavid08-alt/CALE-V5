using Cale.Api.Extensions;
using Cale.Api.Services.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/pqr")]
public sealed class PqrController : ControllerBase
{
    private readonly PqrService _pqr;

    public PqrController(PqrService pqr) => _pqr = pqr;

    [HttpGet("kinds")]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<PqrKindDto>> Kinds() => Ok(PqrService.Kinds());

    /// <summary>Abierto sin sesión para que una cuenta suspendida o un tercero también pueda reclamar.</summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    public async Task<ActionResult<PqrCreatedDto>> Create(CreatePqrRequest request, CancellationToken ct)
    {
        int? userId = User.Identity?.IsAuthenticated == true ? CurrentUser.GetId(User) : null;
        return Ok(await _pqr.CreateAsync(userId, request, ct));
    }

    [HttpGet("track/{code}")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.EmailCode)]
    public async Task<ActionResult<PqrDto>> Track(string code, CancellationToken ct) =>
        Ok(await _pqr.TrackAsync(code, ct));

    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<PqrDto>>> Mine(CancellationToken ct) =>
        Ok(await _pqr.MineAsync(CurrentUser.GetId(User), ct));
}

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/pqr")]
public sealed class AdminPqrController : ControllerBase
{
    private readonly PqrService _pqr;

    public AdminPqrController(PqrService pqr) => _pqr = pqr;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminPqrDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? kind,
        CancellationToken ct) =>
        Ok(await _pqr.AdminListAsync(status, kind, ct));

    [HttpGet("counts")]
    public async Task<ActionResult<PqrCountsDto>> Counts(CancellationToken ct) =>
        Ok(await _pqr.CountsAsync(ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdatePqrRequest request, CancellationToken ct)
    {
        await _pqr.UpdateAsync(id, CurrentUser.GetId(User), request, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/extend")]
    public async Task<IActionResult> Extend(int id, ExtendPqrRequest request, CancellationToken ct)
    {
        await _pqr.ExtendAsync(id, CurrentUser.GetId(User), request, ct);
        return NoContent();
    }
}
