using Cale.Api.Extensions;
using Cale.Api.Services.Play;
using Cale.BuildingBlocks.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/student/play")]
public sealed class PlayController : ControllerBase
{
    private readonly PlayService _play;
    private readonly DuelService _duels;

    public PlayController(PlayService play, DuelService duels)
    {
        _play = play;
        _duels = duels;
    }

    private int UserId => CurrentUser.GetId(User);

    [HttpGet("summary")]
    public Task<PlaySummaryDto> Summary(CancellationToken ct) =>
        _play.GetSummaryAsync(UserId, ct);

    [HttpGet("daily")]
    public Task<DailyChallengeDto> Daily(CancellationToken ct) =>
        _play.GetDailyAsync(UserId, ct);

    [HttpPost("daily/answer")]
    public Task<DailyAnswerResultDto> AnswerDaily(PlayAnswerRequest request, CancellationToken ct) =>
        _play.AnswerDailyAsync(UserId, request, ct);

    [HttpGet("mistakes")]
    public Task<MistakesDto> Mistakes(CancellationToken ct) =>
        _play.GetMistakesAsync(UserId, ct);

    [HttpPost("mistakes/answer")]
    public Task<MistakeAnswerResultDto> AnswerMistake(PlayAnswerRequest request, CancellationToken ct) =>
        _play.AnswerMistakeAsync(UserId, request, ct);

    [HttpGet("readiness")]
    public Task<ReadinessDto> Readiness(CancellationToken ct) =>
        _play.GetReadinessAsync(UserId, ct);

    [HttpGet("achievements")]
    public Task<AchievementsDto> Achievements(CancellationToken ct) =>
        _play.GetAchievementsAsync(UserId, ct);

    [HttpGet("signs")]
    public IReadOnlyList<SignDto> Signs() => _play.GetSigns();

    [HttpGet("signs/questions")]
    public Task<IReadOnlyList<PlayQuestionDto>> SignsQuestions(CancellationToken ct) =>
        _play.GetSignsQuestionsAsync(UserId, ct);

    [HttpPost("signs/check")]
    public Task<QuickCheckResultDto> SignsCheck(PlayAnswerRequest request, CancellationToken ct) =>
        _play.CheckSignsQuestionAsync(UserId, request, ct);

    [HttpPost("signs/result")]
    public Task<GameSavedDto> SignsResult(SignsResultRequest request, CancellationToken ct) =>
        _play.SaveSignsResultAsync(UserId, request, ct);

    [HttpGet("ranking")]
    public Task<RankingDto> Ranking(
        [FromQuery] string? scope,
        [FromQuery] int? groupId,
        CancellationToken ct) =>
        _play.GetRankingAsync(UserId, scope, groupId, ct);

    [HttpPut("ranking/visibility")]
    public async Task<IActionResult> RankingVisibility(RankingVisibilityRequest request, CancellationToken ct)
    {
        await _play.SetRankingVisibilityAsync(UserId, request.ShowInRanking, ct);
        return NoContent();
    }

    [HttpPost("duel")]
    public async Task<DuelStateDto> CreateDuel(CancellationToken ct)
    {
        var questions = await _play.BuildDuelQuestionsAsync(UserId, ct);
        var name = await _play.GetDisplayNameAsync(UserId, ct);
        return _duels.Create(UserId, name, questions);
    }

    [HttpPost("duel/join")]
    public async Task<DuelStateDto> JoinDuel(DuelJoinRequest request, CancellationToken ct)
    {
        var name = await _play.GetDisplayNameAsync(UserId, ct);
        return _duels.Join(UserId, name, request.Code);
    }

    [HttpGet("duel/{code}")]
    public Task<DuelStateDto> Duel(string code, CancellationToken ct) =>
        _duels.GetAsync(UserId, code, ct);

    [HttpPost("duel/{code}/answer")]
    public async Task<DuelAnswerResultDto> AnswerDuel(string code, PlayAnswerRequest request, CancellationToken ct)
    {
        await _play.EnsureNotInOpenAttemptAsync(UserId, request.QuestionId, ct);
        return await _duels.AnswerAsync(UserId, code, request, ct);
    }

    [HttpDelete("duel/{code}")]
    public IActionResult CancelDuel(string code)
    {
        _duels.Cancel(UserId, code);
        return NoContent();
    }
}

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/play")]
public sealed class AdminPlayController : ControllerBase
{
    private readonly PlayService _play;

    public AdminPlayController(PlayService play) => _play = play;

    [HttpGet("signs-report")]
    public Task<SignsReportDto> SignsReport(CancellationToken ct) => _play.GetSignsReportAsync(ct);
}

[ApiController]
[Authorize]
[Route("api/staff/inactive-students")]
public sealed class InactiveStudentsController : ControllerBase
{
    private readonly PlayService _play;

    public InactiveStudentsController(PlayService play) => _play = play;

    [HttpGet]
    public Task<IReadOnlyList<InactiveStudentDto>> List([FromQuery] int days = 7, CancellationToken ct = default) =>
        _play.GetInactiveStudentsAsync(CurrentUser.GetId(User), CurrentUser.GetRole(User), days, ct);

    [HttpPost("remind")]
    public async Task<IActionResult> Remind(RemindStudentsRequest request, CancellationToken ct)
    {
        var sent = await _play.RemindStudentsAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            request.UserIds ?? [],
            ct);
        return Ok(new { sent });
    }
}
