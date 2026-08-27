using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.LiveClassroom.Application.Commands;
using Cale.Modules.LiveClassroom.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/live")]
public sealed class LiveController : ControllerBase
{
    private readonly LiveSessionHandler _handler;

    public LiveController(LiveSessionHandler handler) => _handler = handler;

    [HttpPost("sessions")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<LiveLobbyDto>> Create(
        [FromBody] CreateLiveSessionRequest request,
        CancellationToken ct)
    {
        var lobby = await _handler.CreateAsync(
            request,
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            PublicBaseUrl(),
            ct);
        return Ok(lobby);
    }

    [HttpGet("sessions/{id:int}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<LiveLobbyDto>> GetHost(
        int id,
        CancellationToken ct) =>
        Ok(await _handler.GetHostAsync(
            id,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            PublicBaseUrl(),
            ct));

    [HttpGet("sessions/{id:int}/play")]
    [AllowAnonymous]
    public async Task<ActionResult<LiveLobbyDto>> GetPlay(
        int id,
        [FromQuery] Guid token,
        CancellationToken ct) =>
        Ok(await _handler.GetParticipantViewAsync(id, token, PublicBaseUrl(), ct));

    [HttpPost("sessions/join")]
    [AllowAnonymous]
    public async Task<ActionResult<JoinLiveSessionResponse>> Join(
        [FromBody] JoinLiveSessionRequest request,
        CancellationToken ct)
    {
        int? userId = null;
        string? userName = null;
        if (User?.Identity?.IsAuthenticated == true)
        {
            userId = CurrentUser.GetId(User);
            userName = User.Identity?.Name;
        }

        return Ok(await _handler.JoinAsync(request, userId, userName, ct));
    }

    [HttpPost("sessions/{id:int}/control")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<LiveLobbyDto>> Control(
        int id,
        [FromBody] LiveHostControlRequest request,
        CancellationToken ct) =>
        Ok(await _handler.ControlAsync(
            id,
            request.Action,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            PublicBaseUrl(),
            ct));

    [HttpPost("sessions/{id:int}/questions/{sessionQuestionId:int}/answer")]
    [AllowAnonymous]
    public async Task<IActionResult> Answer(
        int id,
        int sessionQuestionId,
        [FromBody] LiveAnswerRequest request,
        CancellationToken ct)
    {
        await _handler.AnswerAsync(id, sessionQuestionId, request, ct);
        return Ok(new { ok = true });
    }

    private string PublicBaseUrl()
    {
        var configured = HttpContext.RequestServices
            .GetService<IConfiguration>()?["PublicAppUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        var req = HttpContext.Request;
        return $"{req.Scheme}://{req.Host.Value}";
    }
}
