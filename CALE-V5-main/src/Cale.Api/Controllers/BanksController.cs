using Cale.Modules.Catalog.Application.Commands;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/banks")]
public sealed class BanksController : ControllerBase
{
    private readonly ListBanksHandler _list;
    private readonly SaveBankHandler _save;

    public BanksController(ListBanksHandler list, SaveBankHandler save)
    {
        _list = list;
        _save = save;
    }

    [HttpGet]
    [Authorize(Policy = "CatalogReader")]
    public async Task<IActionResult> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default) =>
        Ok(await _list.HandleAsync(activeOnly, ct));

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(
        SaveBankRequest request,
        CancellationToken ct) =>
        Ok(await _save.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(
        int id,
        SaveBankRequest request,
        CancellationToken ct) =>
        Ok(await _save.UpdateAsync(id, request, ct));
}
