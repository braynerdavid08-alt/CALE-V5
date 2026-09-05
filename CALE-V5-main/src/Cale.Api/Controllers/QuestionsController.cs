using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.Catalog.Application.Commands;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

/// <summary>
/// Catálogo global del administrador. Escuela/docente con plan activo: lectura.
/// </summary>
[ApiController]
[Authorize]
[Route("api/questions")]
public sealed class QuestionsController : ControllerBase
{
    private readonly ListQuestionsHandler _list;
    private readonly ListQuestionsForReviewHandler _listReview;
    private readonly GetQuestionHandler _get;
    private readonly SaveQuestionHandler _save;
    private readonly ListBlocksHandler _blocks;
    private readonly ICatalogAccessGuard _access;

    public QuestionsController(
        ListQuestionsHandler list,
        ListQuestionsForReviewHandler listReview,
        GetQuestionHandler get,
        SaveQuestionHandler save,
        ListBlocksHandler blocks,
        ICatalogAccessGuard access)
    {
        _list = list;
        _listReview = listReview;
        _get = get;
        _save = save;
        _blocks = blocks;
        _access = access;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? bankId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? active = null,
        CancellationToken ct = default)
    {
        await _access.EnsureCatalogReadAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            ct);
        return Ok(await _list.HandleAsync(
            page, pageSize, bankId, search, active, ownerId: null, ct));
    }

    [HttpGet("review")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Review(
        [FromQuery] int bankId,
        CancellationToken ct)
    {
        if (bankId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Indica el banco a revisar.",
                Detail = "bank_required",
                Status = 400
            });
        }

        await _access.EnsureCatalogReadAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            ct);
        return Ok(await _listReview.HandleAsync(bankId, ct));
    }

    [HttpGet("blocks")]
    public async Task<IActionResult> Blocks(CancellationToken ct)
    {
        await _access.EnsureCatalogReadAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            ct);
        return Ok(await _blocks.HandleAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        await _access.EnsureCatalogReadAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            ct);
        return Ok(await _get.HandleAsync(id, ct));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(
        SaveQuestionRequest request,
        CancellationToken ct)
    {
        var id = await _save.CreateAsync(request, CurrentUser.GetId(User), ct);
        return Ok(new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Update(
        int id,
        SaveQuestionRequest request,
        CancellationToken ct)
    {
        await _save.UpdateAsync(
            id,
            request,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct);
        return NoContent();
    }
}
