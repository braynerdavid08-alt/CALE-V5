using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.LiveClassroom.Application.Commands;
using Cale.Modules.LiveClassroom.Application.DTOs;
using Cale.Modules.Presentation.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/live")]
public sealed class LiveController : ControllerBase
{
    private readonly LiveSessionHandler _handler;
    private readonly IPresentationStore _presentations;

    public LiveController(LiveSessionHandler handler, IPresentationStore presentations)
    {
        _handler = handler;
        _presentations = presentations;
    }

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
    [Authorize]
    public async Task<ActionResult<LiveLobbyDto>> GetPlay(
        int id,
        [FromQuery] Guid token,
        CancellationToken ct) =>
        Ok(await _handler.GetParticipantViewAsync(id, token, PublicBaseUrl(), ct));

    [HttpGet("sessions/{id:int}/presentation")]
    [Authorize]
    public async Task<ActionResult<LivePresentationDto>> GetPresentation(
        int id,
        [FromQuery] Guid token,
        CancellationToken ct)
    {
        var (presentationId, slideIndex) = await _handler.ResolveLinkedPresentationAsync(id, token, ct);
        var deck = await _presentations.GetWithSlidesAsync(presentationId, ct)
            ?? throw new NotFoundException("Presentación no encontrada.");
        if (!deck.IsActive)
        {
            throw new NotFoundException("Presentación no encontrada.");
        }

        var slides = deck.Slides
            .OrderBy(s => s.Position)
            .Select(s => new LivePresentationSlideDto(
                s.Position,
                s.Title,
                s.BackgroundJson,
                s.ElementsJson))
            .ToList();

        return Ok(new LivePresentationDto(
            deck.Id,
            deck.Title,
            Math.Clamp(slideIndex, 0, Math.Max(0, slides.Count - 1)),
            slides.Count,
            slides));
    }

    [HttpPost("sessions/join")]
    [Authorize]
    public async Task<ActionResult<JoinLiveSessionResponse>> Join(
        [FromBody] JoinLiveSessionRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var userName = User.Identity?.Name;
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
            ct,
            request.QuickQuestion));

    [HttpGet("sessions/{id:int}/export")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Export(
        int id,
        CancellationToken ct)
    {
        var (bytes, fileName) = await _handler.ExportResultsCsvAsync(
            id,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct);
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpPost("sessions/{id:int}/questions/{sessionQuestionId:int}/answer")]
    [Authorize]
    public async Task<IActionResult> Answer(
        int id,
        int sessionQuestionId,
        [FromBody] LiveAnswerRequest request,
        CancellationToken ct) =>
        Ok(await _handler.AnswerAsync(id, sessionQuestionId, request, ct));

    [HttpGet("sessions/{id:int}/doubts")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<LiveDoubtDto>>> ListDoubts(
        int id,
        [FromQuery] Guid? token,
        CancellationToken ct) =>
        Ok(await _handler.ListDoubtsAsync(id, token, ct));

    [HttpPost("sessions/{id:int}/doubts")]
    [Authorize]
    public async Task<ActionResult<LiveDoubtDto>> PostDoubt(
        int id,
        [FromBody] LiveDoubtRequest request,
        CancellationToken ct) =>
        Ok(await _handler.PostDoubtAsync(id, request, ct));

    [HttpPost("sessions/{id:int}/doubts/{doubtId:int}/vote")]
    [Authorize]
    public async Task<ActionResult<LiveDoubtDto>> VoteDoubt(
        int id,
        int doubtId,
        [FromBody] LiveDoubtVoteRequest request,
        CancellationToken ct) =>
        Ok(await _handler.VoteDoubtAsync(id, doubtId, request, ct));

    [HttpPost("sessions/{id:int}/doubts/{doubtId:int}/resolve")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<LiveDoubtDto>> ResolveDoubt(
        int id,
        int doubtId,
        CancellationToken ct) =>
        Ok(await _handler.ResolveDoubtAsync(
            id,
            doubtId,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct));

    [HttpGet("sessions/{id:int}/analytics")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<LiveAnalyticsDto>> Analytics(
        int id,
        CancellationToken ct) =>
        Ok(await _handler.GetAnalyticsAsync(
            id,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct));

    [HttpPost("sessions/{id:int}/rematch")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<LiveRematchResponse>> Rematch(
        int id,
        CancellationToken ct) =>
        Ok(await _handler.RematchAsync(
            id,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            PublicBaseUrl(),
            ct));

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
