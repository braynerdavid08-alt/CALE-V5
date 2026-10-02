using Cale.Api.Extensions;
using Cale.Api.Services.Assistant;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

public sealed record AssistantChatRequest(IReadOnlyList<AssistantChatMessage> Messages);

[ApiController]
[Authorize]
[Route("api/assistant")]
public sealed class AssistantController : ControllerBase
{
    private readonly AssistantService _assistant;
    private readonly IUserStore _users;

    public AssistantController(AssistantService assistant, IUserStore users)
    {
        _assistant = assistant;
        _users = users;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) =>
        Ok(_assistant.Status(await CurrentAsync(ct)));

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(AssistantChatRequest request, CancellationToken ct) =>
        Ok(await _assistant.ChatAsync(await CurrentAsync(ct), request.Messages ?? [], ct));

    [HttpGet("quick/{key}")]
    public async Task<IActionResult> Quick(string key, CancellationToken ct) =>
        Ok(await _assistant.QuickAsync(await CurrentAsync(ct), key, ct));

    [HttpPost("actions/{id}/confirm")]
    public async Task<IActionResult> Confirm(string id, CancellationToken ct) =>
        Ok(await _assistant.ConfirmAsync(await CurrentAsync(ct), id, ct));

    [HttpPost("actions/{id}/discard")]
    public async Task<IActionResult> Discard(string id, CancellationToken ct)
    {
        _assistant.Discard(await CurrentAsync(ct), id);
        return NoContent();
    }

    private async Task<AssistantUser> CurrentAsync(CancellationToken ct)
    {
        var id = CurrentUser.GetId(User);
        var role = CurrentUser.GetRole(User);
        var user = await _users.GetByIdAsync(id, ct);
        var schoolUserId = role == Roles.School ? id : user?.SchoolId;
        return new AssistantUser(id, role, user?.Name ?? "", schoolUserId);
    }
}
