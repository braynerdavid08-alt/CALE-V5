using Cale.Api.Extensions;
using Cale.Modules.Engagement.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ListNotificationsHandler _handler;

    public NotificationsController(ListNotificationsHandler handler) =>
        _handler = handler;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _handler.HandleAsync(CurrentUser.GetId(User), ct));

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct)
    {
        await _handler.MarkReadAsync(id, CurrentUser.GetId(User), ct);
        return NoContent();
    }
}
