using Cale.Api.Extensions;
using Cale.Modules.Assessment.Application.Commands;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Assessment.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ratings")]
public sealed class RatingsController : ControllerBase
{
    private readonly SaveRatingHandler _save;
    private readonly ManageRatingHandler _manage;
    private readonly ListRatingsHandler _list;

    public RatingsController(
        SaveRatingHandler save,
        ManageRatingHandler manage,
        ListRatingsHandler list)
    {
        _save = save;
        _manage = manage;
        _list = list;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        SaveRatingRequest request,
        CancellationToken ct)
    {
        await _save.HandleAsync(request, CurrentUser.GetId(User), ct);
        return NoContent();
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _list.HandleAsync(ct));

    [HttpPatch("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Manage(
        int id,
        ManageRatingRequest request,
        CancellationToken ct)
    {
        await _manage.HandleAsync(id, request, ct);
        return NoContent();
    }
}
