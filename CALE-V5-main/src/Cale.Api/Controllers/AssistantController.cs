using Cale.Api.Extensions;
using Cale.Api.Services.Assistant;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
        Ok(await _assistant.StatusAsync(await CurrentAsync(ct), ct));

    [HttpGet("usage/school")]
    [Authorize(Policy = "SchoolOnly")]
    public async Task<IActionResult> SchoolUsage(
        [FromServices] IAssistantUsageStore usage,
        [FromServices] Microsoft.Extensions.Options.IOptions<AssistantOptions> options,
        CancellationToken ct)
    {
        var user = await CurrentAsync(ct);
        var schoolId = user.SchoolUserId ?? user.UserId;
        return Ok(await usage.GetSchoolSummaryAsync(
            schoolId,
            options.Value.DailyMessagesPerUser,
            ct));
    }

    [HttpGet("usage/admin")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminUsage(
        [FromServices] IAssistantUsageStore usage,
        [FromServices] Microsoft.Extensions.Options.IOptions<AssistantOptions> options,
        [FromQuery] int? schoolUserId,
        CancellationToken ct)
    {
        var limit = options.Value.DailyMessagesPerUser;
        if (schoolUserId is int id)
        {
            return Ok(await usage.GetSchoolSummaryAsync(id, limit, ct));
        }

        return Ok(await usage.ListSchoolsTodayAsync(limit, ct));
    }

    [HttpPost("chat")]
    [EnableRateLimiting(RateLimitPolicies.Assistant)]
    public async Task<IActionResult> Chat(AssistantChatRequest request, CancellationToken ct) =>
        Ok(await _assistant.ChatAsync(await CurrentAsync(ct), request.Messages ?? [], ct));

    [HttpGet("quick/{key}")]
    public async Task<IActionResult> Quick(string key, CancellationToken ct) =>
        Ok(await _assistant.QuickAsync(await CurrentAsync(ct), key, ct));

    [HttpPost("actions/{id}/confirm")]
    public async Task<IActionResult> Confirm(string id, CancellationToken ct) =>
        Ok(await _assistant.ConfirmAsync(await CurrentAsync(ct), id, ct));

    /// <summary>
    /// Student selected an exam slot in the chat UI; creates the same pending
    /// <c>agendar_examen</c> action used by the LLM path (still requires confirm).
    /// </summary>
    [HttpPost("actions/prepare-exam")]
    [EnableRateLimiting(RateLimitPolicies.Assistant)]
    public async Task<IActionResult> PrepareExam(AssistantPrepareExamRequest request, CancellationToken ct) =>
        Ok(await _assistant.PrepareExamBookingAsync(
            await CurrentAsync(ct),
            request.Date ?? "",
            request.Time ?? "",
            ct));

    /// <summary>
    /// Student selected a theory or practical session card; prepares the matching
    /// confirmable write tool (still requires Confirm).
    /// </summary>
    [HttpPost("actions/prepare-session")]
    [EnableRateLimiting(RateLimitPolicies.Assistant)]
    public async Task<IActionResult> PrepareSession(AssistantPrepareSessionRequest request, CancellationToken ct) =>
        Ok(await _assistant.PrepareSessionBookingAsync(
            await CurrentAsync(ct),
            request.SessionId,
            request.Tool ?? "",
            ct));

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
