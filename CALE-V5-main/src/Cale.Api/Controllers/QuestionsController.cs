using Cale.Api.Extensions;
using Cale.Modules.Catalog.Application.Commands;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

/// <summary>
/// Catálogo jerárquico: solo Admin crea/edita.
/// Escuela y Docente heredan el catálogo en lectura.
/// </summary>
[ApiController]
[Authorize(Policy = "CatalogReader")]
[Route("api/questions")]
public sealed class QuestionsController : ControllerBase
{
    private readonly ListQuestionsHandler _list;
    private readonly GetQuestionHandler _get;
    private readonly SaveQuestionHandler _save;
    private readonly ListBlocksHandler _blocks;

    public QuestionsController(
        ListQuestionsHandler list,
        GetQuestionHandler get,
        SaveQuestionHandler save,
        ListBlocksHandler blocks)
    {
        _list = list;
        _get = get;
        _save = save;
        _blocks = blocks;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? bankId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? active = null,
        CancellationToken ct = default) =>
        // Herencia: escuela/docente ven todo el catálogo del Admin (sin filtro por dueño).
        Ok(await _list.HandleAsync(
            page, pageSize, bankId, search, active, ownerId: null, ct));

    [HttpGet("blocks")]
    public async Task<IActionResult> Blocks(CancellationToken ct) =>
        Ok(await _blocks.HandleAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(await _get.HandleAsync(id, ct));

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
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(
        int id,
        SaveQuestionRequest request,
        CancellationToken ct)
    {
        await _save.UpdateAsync(
            id,
            request,
            CurrentUser.GetId(User),
            isAdmin: true,
            ct);
        return NoContent();
    }
}
