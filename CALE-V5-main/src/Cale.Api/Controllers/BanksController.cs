using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.Catalog.Application.Commands;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/banks")]
public sealed class BanksController : ControllerBase
{
    private readonly ListBanksHandler _list;
    private readonly SaveBankHandler _save;
    private readonly ICatalogAccessGuard _access;

    public BanksController(
        ListBanksHandler list,
        SaveBankHandler save,
        ICatalogAccessGuard access)
    {
        _list = list;
        _save = save;
        _access = access;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool activeOnly = false,
        [FromQuery] bool includeThemes = false,
        CancellationToken ct = default)
    {
        var userId = CurrentUser.GetId(User);
        var role = CurrentUser.GetRole(User);
        if (role is Roles.Admin or Roles.School or Roles.Teacher)
        {
            await _access.EnsureCatalogReadAsync(userId, role, ct);
        }
        else
        {
            await _access.EnsureSimulacroAsync(userId, role, ct);
        }

        return Ok(await _list.HandleAsync(
            activeOnly,
            ct,
            includeThemes,
            userId,
            role == Roles.Admin));
    }

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
