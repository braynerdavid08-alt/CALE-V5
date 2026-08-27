using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.Modules.Engagement.Application.DTOs;
using Cale.Modules.Engagement.Application.Queries;
using Cale.Modules.Identity.Application.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ListNotificationsHandler _handler;
    private readonly BroadcastNotificationHandler _broadcast;

    public NotificationsController(
        ListNotificationsHandler handler,
        BroadcastNotificationHandler broadcast)
    {
        _handler = handler;
        _broadcast = broadcast;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool? unreadOnly,
        [FromQuery] string? category,
        [FromQuery] string? type,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 30,
        CancellationToken ct = default) =>
        Ok(await _handler.HandleAsync(
            CurrentUser.GetId(User),
            unreadOnly,
            category,
            type,
            skip,
            take,
            ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Ok(new { count = await _handler.CountUnreadAsync(CurrentUser.GetId(User), ct) });

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct)
    {
        await _handler.MarkReadAsync(id, CurrentUser.GetId(User), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        var count = await _handler.MarkAllReadAsync(CurrentUser.GetId(User), ct);
        return Ok(new { marked = count });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        await _handler.ArchiveAsync(id, CurrentUser.GetId(User), ct);
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct) =>
        Ok(await _handler.GetPreferencesAsync(CurrentUser.GetId(User), ct));

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateNotificationPreferenceRequest request,
        CancellationToken ct) =>
        Ok(await _handler.UpdatePreferencesAsync(
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("broadcast")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Broadcast(
        [FromBody] BroadcastNotificationRequest request,
        CancellationToken ct) =>
        Ok(await _broadcast.HandleAsync(request, ct));
}
