using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/game-show")]
public sealed class GameShowController : ControllerBase
{
    private readonly GameShowHandler _handler;

    public GameShowController(GameShowHandler handler) => _handler = handler;

    [HttpPost]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateGameShowRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var role = CurrentUser.GetRole(User);
        int? schoolId = role == Roles.School ? userId : null;
        return Ok(await _handler.CreateAsync(userId, schoolId, request, ct));
    }

    [HttpGet("mine")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var role = CurrentUser.GetRole(User);
        return Ok(await _handler.ListHistoryAsync(userId, role, ct));
    }

    [HttpGet("school")]
    [Authorize(Policy = "SchoolOnly")]
    public async Task<IActionResult> SchoolHistory(CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        return Ok(await _handler.ListHistoryAsync(userId, Roles.School, ct));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(
        int id,
        [FromQuery] Guid? playerToken,
        [FromQuery] bool host = false,
        CancellationToken ct = default)
    {
        int? userId = null;
        try { userId = CurrentUser.GetId(User); } catch { /* anonymous screen/player */ }
        return Ok(await _handler.GetLobbyAsync(id, userId, playerToken, ct, preferHostView: host));
    }

    [HttpPost("join")]
    [AllowAnonymous]
    public async Task<IActionResult> Join(
        [FromBody] JoinGameShowRequest request,
        CancellationToken ct)
    {
        int? userId = null;
        try { userId = CurrentUser.GetId(User); } catch { /* optional */ }
        return Ok(await _handler.JoinAsync(request, userId, ct));
    }

    [HttpGet("packs/oficial")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> OfficialPack(CancellationToken ct = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "gameshow-100-dijeron-oficial.json");
        if (!System.IO.File.Exists(path))
        {
            // Dev fallback: look relative to content root.
            path = Path.Combine(Directory.GetCurrentDirectory(), "SeedData", "gameshow-100-dijeron-oficial.json");
        }
        if (!System.IO.File.Exists(path))
        {
            return NotFound(new { error = "pack_missing", message = "Pack oficial no disponible." });
        }

        var text = await System.IO.File.ReadAllTextAsync(path, ct);
        var body = GameShowQuestionImport.ParseJson(text);
        return Ok(body);
    }

    [HttpGet("{id:int}/export")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> ExportQuestions(
        int id,
        [FromQuery] string format = "csv",
        CancellationToken ct = default)
    {
        var (bytes, fileName, contentType) = await _handler.ExportQuestionsAsync(
            id,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            format,
            ct);
        return File(bytes, contentType, fileName);
    }

    /// <summary>
    /// Parses an exported JSON/CSV question pack into a create payload (draft preview).
    /// Does not create a session — client fills the form and calls POST /api/game-show.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = "TeacherOrAdmin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> ImportQuestions(
        IFormFile? file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "empty_import", message = "Selecciona un archivo JSON o CSV." });
        }

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(ct);
        var body = GameShowQuestionImport.Parse(text, file.FileName);
        return Ok(body);
    }

    /// <summary>
    /// Parses a question pack and creates the session in one step.
    /// </summary>
    [HttpPost("import/create")]
    [Authorize(Policy = "TeacherOrAdmin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> ImportAndCreate(
        IFormFile? file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "empty_import", message = "Selecciona un archivo JSON o CSV." });
        }

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(ct);
        var body = GameShowQuestionImport.Parse(text, file.FileName);

        var userId = CurrentUser.GetId(User);
        var role = CurrentUser.GetRole(User);
        int? schoolId = role == Roles.School ? userId : null;
        return Ok(await _handler.CreateAsync(userId, schoolId, body, ct));
    }

    [HttpGet("school/{id:int}/export")]
    [Authorize(Policy = "SchoolOnly")]
    public async Task<IActionResult> ExportQuestionsForSchool(
        int id,
        [FromQuery] string format = "csv",
        CancellationToken ct = default)
    {
        var (bytes, fileName, contentType) = await _handler.ExportQuestionsAsync(
            id,
            CurrentUser.GetId(User),
            isAdmin: false,
            format,
            ct);
        return File(bytes, contentType, fileName);
    }

    [HttpPost("{id:int}/start")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        await _handler.StartAsync(id, CurrentUser.GetId(User), ct);
        return Ok(await _handler.GetLobbyAsync(id, CurrentUser.GetId(User), null, ct, preferHostView: true));
    }

    [HttpPost("{id:int}/pause")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Pause(int id, CancellationToken ct)
    {
        await _handler.PauseAsync(id, CurrentUser.GetId(User), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/resume")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Resume(int id, CancellationToken ct)
    {
        await _handler.ResumeAsync(id, CurrentUser.GetId(User), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/buzz")]
    [AllowAnonymous]
    public async Task<IActionResult> Buzz(
        int id,
        [FromQuery] Guid playerToken,
        CancellationToken ct)
    {
        await _handler.BuzzAsync(id, playerToken, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/force-buzz")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> ForceBuzz(
        int id,
        [FromBody] ForceBuzzRequest body,
        CancellationToken ct)
    {
        await _handler.ForceBuzzWinnerAsync(id, CurrentUser.GetId(User), body.Team, ct);
        return Ok(await _handler.GetLobbyAsync(id, CurrentUser.GetId(User), null, ct, preferHostView: true));
    }

    [HttpPost("{id:int}/answer")]
    [AllowAnonymous]
    public async Task<IActionResult> Answer(
        int id,
        [FromQuery] Guid playerToken,
        [FromBody] AnswerGameShowRequest request,
        CancellationToken ct)
    {
        await _handler.AnswerAsync(id, playerToken, request, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reveal/{answerId:int}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Reveal(int id, int answerId, CancellationToken ct)
    {
        await _handler.HostRevealAsync(id, CurrentUser.GetId(User), answerId, ct);
        return Ok(await _handler.GetLobbyAsync(id, CurrentUser.GetId(User), null, ct, preferHostView: true));
    }

    [HttpPost("{id:int}/strike")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Strike(int id, CancellationToken ct)
    {
        await _handler.HostStrikeAsync(id, CurrentUser.GetId(User), ct);
        return Ok(await _handler.GetLobbyAsync(id, CurrentUser.GetId(User), null, ct, preferHostView: true));
    }

    [HttpPost("{id:int}/fail-steal")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> FailSteal(int id, CancellationToken ct)
    {
        await _handler.HostFailStealAsync(id, CurrentUser.GetId(User), ct);
        return Ok(await _handler.GetLobbyAsync(id, CurrentUser.GetId(User), null, ct, preferHostView: true));
    }

    [HttpPost("{id:int}/end-round")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> EndRound(int id, CancellationToken ct)
    {
        await _handler.HostEndRoundAsync(id, CurrentUser.GetId(User), ct);
        return Ok(await _handler.GetLobbyAsync(id, CurrentUser.GetId(User), null, ct, preferHostView: true));
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Assign(
        int id,
        [FromBody] AssignPlayerRequest request,
        CancellationToken ct)
    {
        await _handler.AssignPlayerAsync(id, CurrentUser.GetId(User), request, ct);
        return Ok(await _handler.GetLobbyAsync(id, CurrentUser.GetId(User), null, ct, preferHostView: true));
    }

    [HttpPost("{id:int}/next-round")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> NextRound(int id, CancellationToken ct)
    {
        await _handler.NextRoundAsync(id, CurrentUser.GetId(User), ct);
        return Ok(await _handler.GetLobbyAsync(id, CurrentUser.GetId(User), null, ct, preferHostView: true));
    }

    [HttpPost("{id:int}/finish")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Finish(int id, CancellationToken ct)
    {
        await _handler.FinishAsync(id, CurrentUser.GetId(User), ct);
        return Ok(await _handler.GetLobbyAsync(id, CurrentUser.GetId(User), null, ct, preferHostView: true));
    }
}
