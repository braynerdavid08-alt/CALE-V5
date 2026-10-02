using Cale.Api.Services.Admin;
using Cale.BuildingBlocks.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/insights")]
public sealed class AdminInsightsController : ControllerBase
{
    private readonly AdminInsightsService _insights;

    public AdminInsightsController(AdminInsightsService insights) => _insights = insights;

    [HttpGet("weekly")]
    public Task<WeeklySummaryDto> Weekly([FromQuery] int days = 7, CancellationToken ct = default) =>
        _insights.GetWeeklySummaryAsync(days, ct);

    [HttpGet("storage")]
    public Task<StorageReportDto> Storage(CancellationToken ct) => _insights.GetStorageAsync(ct);

    [HttpGet("hardest-questions")]
    public Task<IReadOnlyList<QuestionAccuracyDto>> HardestQuestions([FromQuery] int take = 30, CancellationToken ct = default) =>
        _insights.GetHardestQuestionsAsync(take, ct);
}
