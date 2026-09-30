using Cale.Api.Extensions;
using Cale.Api.Services.Admin;
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

    [HttpGet("usage")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Usage(
        [FromServices] BankUsageService usage,
        CancellationToken ct) =>
        Ok(await usage.ListAsync(ct));

    [HttpPut("{id:int}/official")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetOfficial(
        int id,
        SetBankOfficialRequest request,
        [FromServices] BankUsageService usage,
        CancellationToken ct)
    {
        await usage.SetOfficialAsync(id, request.Official, CurrentUser.GetId(User), ct);
        return NoContent();
    }

    public sealed record SetBankOfficialRequest(bool Official);

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

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(
        int id,
        [FromServices] CatalogPurgeService purge,
        CancellationToken ct)
    {
        await purge.PurgeBankAsync(id, ct);
        return NoContent();
    }
}
