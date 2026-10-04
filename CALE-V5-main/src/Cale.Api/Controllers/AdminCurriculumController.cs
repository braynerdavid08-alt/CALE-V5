using Cale.Api.Services.Courses;
using Cale.BuildingBlocks.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

/// <summary>Reviews and applies the built-in curriculum on the platform courses. School courses are never touched.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/curriculum")]
public sealed class AdminCurriculumController : ControllerBase
{
    private readonly CurriculumSync _sync;

    public AdminCurriculumController(CurriculumSync sync) => _sync = sync;

    /// <summary>Dry run: what applying would change, without writing anything.</summary>
    [HttpGet("plan")]
    public Task<CurriculumPlanDto> Plan(CancellationToken ct) => _sync.PlanAsync(ct);

    [HttpPost("apply")]
    public Task<CurriculumPlanDto> Apply(CurriculumApplyRequest request, CancellationToken ct) =>
        _sync.ApplyAsync(request.ResetProgress, ct);
}

public sealed record CurriculumApplyRequest(bool ResetProgress);
