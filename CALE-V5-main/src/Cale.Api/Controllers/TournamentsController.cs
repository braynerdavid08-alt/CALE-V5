using Cale.Api.Extensions;
using Cale.Api.Services.Tournaments;
using Cale.BuildingBlocks.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tournaments")]
public sealed class TournamentsController : ControllerBase
{
    private readonly TournamentService _tournaments;

    public TournamentsController(TournamentService tournaments) => _tournaments = tournaments;

    private int UserId => CurrentUser.GetId(User);
    private string Role => CurrentUser.GetRole(User);

    [HttpGet]
    public Task<IReadOnlyList<TournamentListItemDto>> List(CancellationToken ct) =>
        _tournaments.ListForActorAsync(UserId, Role, ct);

    [HttpPost]
    [Authorize(Roles = Roles.Teacher)]
    public Task<TournamentListItemDto> Create([FromBody] CreateTournamentRequest request, CancellationToken ct) =>
        _tournaments.CreateAsync(UserId, request, ct);

    [HttpGet("{id:int}")]
    public Task<TournamentLobbyDto> Lobby(int id, CancellationToken ct) =>
        _tournaments.GetLobbyAsync(id, UserId, ct);

    [HttpPost("{id:int}/join")]
    [Authorize(Roles = Roles.Student)]
    public Task<TournamentLobbyDto> Join(int id, CancellationToken ct) =>
        _tournaments.JoinAsync(id, UserId, ct);

    [HttpPost("{id:int}/close-registration")]
    [Authorize(Roles = Roles.Teacher + "," + Roles.School + "," + Roles.Admin)]
    public async Task<IActionResult> CloseRegistration(int id, CancellationToken ct)
    {
        await _tournaments.CloseRegistrationAsync(id, UserId, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/start")]
    [Authorize(Roles = Roles.Teacher + "," + Roles.School + "," + Roles.Admin)]
    public Task<TournamentBracketDto> Start(int id, CancellationToken ct) =>
        _tournaments.StartAsync(id, UserId, ct);

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = Roles.Teacher + "," + Roles.School + "," + Roles.Admin)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _tournaments.CancelAsync(id, UserId, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/bracket")]
    public Task<TournamentBracketDto> Bracket(int id, CancellationToken ct) =>
        _tournaments.GetBracketAsync(id, UserId, ct);

    [HttpGet("matches/{matchId:int}")]
    public Task<TournamentMatchViewDto> Match(int matchId, CancellationToken ct) =>
        _tournaments.GetMatchAsync(matchId, UserId, ct);

    [HttpPost("matches/{matchId:int}/answer")]
    [Authorize(Roles = Roles.Student)]
    public Task<TournamentAnswerResultDto> Answer(
        int matchId,
        [FromBody] TournamentAnswerRequest request,
        CancellationToken ct) =>
        _tournaments.AnswerAsync(matchId, UserId, request, ct);

    [HttpGet("me/trophies")]
    public Task<IReadOnlyList<TournamentTrophyDto>> MyTrophies(CancellationToken ct) =>
        _tournaments.ListTrophiesAsync(UserId, ct);

    [HttpGet("me/profile")]
    public Task<CompetitiveProfileDto> MyProfile(CancellationToken ct) =>
        _tournaments.GetCompetitiveProfileAsync(UserId, ct);
}
