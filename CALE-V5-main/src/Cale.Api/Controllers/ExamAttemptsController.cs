using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.Modules.Assessment.Application.Commands;
using Cale.Modules.Assessment.Application.DTOs;
using Cale.Modules.Assessment.Application.Queries;
using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exams")]
public sealed class ExamAttemptsController : ControllerBase
{
    private readonly StartExamHandler _start;
    private readonly AnswerQuestionHandler _answer;
    private readonly FinishExamHandler _finish;
    private readonly ReviewAttemptHandler _review;
    private readonly ICatalogAccessGuard _access;
    private readonly TheoryTrainingService _theory;

    public ExamAttemptsController(
        StartExamHandler start,
        AnswerQuestionHandler answer,
        FinishExamHandler finish,
        ReviewAttemptHandler review,
        ICatalogAccessGuard access,
        TheoryTrainingService theory)
    {
        _start = start;
        _answer = answer;
        _finish = finish;
        _review = review;
        _access = access;
        _theory = theory;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(
        StartExamRequest request,
        CancellationToken ct)
    {
        await _access.EnsureSimulacroAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            ct);
        return Ok(await _start.HandleAsync(request, CurrentUser.GetId(User), ct));
    }

    [HttpPost("{attemptId:int}/answer")]
    public async Task<IActionResult> Answer(
        int attemptId,
        AnswerRequest request,
        CancellationToken ct)
    {
        await _answer.HandleAsync(attemptId, CurrentUser.GetId(User), request, ct);
        return NoContent();
    }

    [HttpPost("{attemptId:int}/finish")]
    public async Task<IActionResult> Finish(int attemptId, CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var result = await _finish.HandleAsync(attemptId, userId, ct);
        if (result.Passed && result.ExamId is int examId)
        {
            await _theory.OnPlatformTheoryExamPassedAsync(userId, examId, ct);
        }

        return Ok(result);
    }

    [HttpGet("{attemptId:int}/review")]
    public async Task<IActionResult> Review(int attemptId, CancellationToken ct) =>
        Ok(await _review.HandleAsync(
            attemptId,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct));
}
