using Cale.Api.Extensions;
using Cale.Api.Services.Legal;
using Cale.Modules.Identity.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/legal")]
public sealed class LegalController : ControllerBase
{
    private readonly LegalService _legal;
    private readonly MinorConsentService _minors;

    public LegalController(LegalService legal, MinorConsentService minors)
    {
        _legal = legal;
        _minors = minors;
    }

    [HttpGet("me/age")]
    [Authorize]
    public async Task<ActionResult<MyAgeStatusDto>> MyAge(CancellationToken ct) =>
        Ok(await _minors.StatusAsync(CurrentUser.GetId(User), CurrentUser.GetRole(User), ct));

    [HttpPost("me/age")]
    [Authorize(Policy = "StudentOnly")]
    public async Task<ActionResult<MyAgeStatusDto>> DeclareAge(DeclareAgeRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        await _minors.DeclareAsync(
            userId, request.BirthDate, request.Guardian,
            LegalEvidence.Ip(HttpContext), LegalEvidence.UserAgent(HttpContext), ct);
        return Ok(await _minors.StatusAsync(userId, CurrentUser.GetRole(User), ct));
    }

    [HttpPost("me/guardian")]
    [Authorize(Policy = "StudentOnly")]
    [EnableRateLimiting(RateLimitPolicies.EmailCode)]
    public async Task<ActionResult<MyAgeStatusDto>> UpdateGuardian(GuardianDto request, CancellationToken ct) =>
        Ok(await _minors.UpdateGuardianAsync(CurrentUser.GetId(User), CurrentUser.GetRole(User), request, ct));

    [HttpGet("guardian/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.EmailCode)]
    public async Task<ActionResult<GuardianViewDto>> GuardianView(string token, CancellationToken ct) =>
        Ok(await _minors.GuardianViewAsync(token, ct));

    [HttpPost("guardian/{token}/decision")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.EmailCode)]
    public async Task<ActionResult<GuardianViewDto>> GuardianDecision(
        string token,
        GuardianDecisionRequest request,
        CancellationToken ct) =>
        Ok(await _minors.DecideAsync(
            token, request, LegalEvidence.Ip(HttpContext), LegalEvidence.UserAgent(HttpContext), ct));

    [HttpGet("documents")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<LegalDocumentSummaryDto>>> Documents(CancellationToken ct) =>
        Ok(await _legal.ListPublicAsync(ct));

    [HttpGet("documents/{key}")]
    [AllowAnonymous]
    public async Task<ActionResult<LegalDocumentDto>> Document(string key, CancellationToken ct) =>
        Ok(await _legal.GetPublicAsync(key, ct));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MyLegalStatusDto>> MyStatus(CancellationToken ct) =>
        Ok(await _legal.StatusAsync(CurrentUser.GetId(User), CurrentUser.GetRole(User), ct));

    [HttpGet("me/history")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<LegalAcceptanceDto>>> MyHistory(CancellationToken ct) =>
        Ok(await _legal.HistoryAsync(CurrentUser.GetId(User), ct));

    [HttpPost("me/accept")]
    [Authorize]
    public async Task<ActionResult<MyLegalStatusDto>> Accept(LegalConsentDto consent, CancellationToken ct) =>
        Ok(await _legal.AcceptAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            consent,
            LegalEvidence.Ip(HttpContext),
            LegalEvidence.UserAgent(HttpContext),
            ct));
}

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/legal")]
public sealed class AdminLegalController : ControllerBase
{
    private readonly LegalService _legal;
    private readonly MinorConsentService _minors;

    public AdminLegalController(LegalService legal, MinorConsentService minors)
    {
        _legal = legal;
        _minors = minors;
    }

    [HttpGet("minors")]
    public async Task<ActionResult<IReadOnlyList<AdminMinorDto>>> Minors(CancellationToken ct) =>
        Ok(await _minors.AdminListAsync(ct));

    [HttpPost("minors/{userId:int}/resend")]
    public async Task<IActionResult> ResendGuardian(int userId, CancellationToken ct)
    {
        await _minors.AdminResendAsync(userId, ct);
        return NoContent();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<IReadOnlyList<LegalSummaryDto>>> Summary(CancellationToken ct) =>
        Ok(await _legal.SummaryAsync(ct));

    [HttpGet("versions")]
    public async Task<ActionResult<IReadOnlyList<LegalVersionDto>>> Versions(CancellationToken ct) =>
        Ok(await _legal.ListVersionsAsync(ct));

    [HttpPost("versions")]
    public async Task<ActionResult<LegalVersionDto>> CreateDraft(SaveLegalVersionRequest request, CancellationToken ct) =>
        Ok(await _legal.CreateDraftAsync(request, CurrentUser.GetId(User), ct));

    [HttpPut("versions/{id:int}")]
    public async Task<ActionResult<LegalVersionDto>> UpdateDraft(int id, SaveLegalVersionRequest request, CancellationToken ct) =>
        Ok(await _legal.UpdateDraftAsync(id, request, ct));

    [HttpPost("versions/{id:int}/publish")]
    public async Task<ActionResult<LegalVersionDto>> Publish(int id, CancellationToken ct) =>
        Ok(await _legal.PublishAsync(id, CurrentUser.GetId(User), ct));

    [HttpGet("acceptances")]
    public async Task<ActionResult<IReadOnlyList<LegalAcceptanceDto>>> Acceptances(
        [FromQuery] string? document,
        [FromQuery] int take = 200,
        CancellationToken ct = default) =>
        Ok(await _legal.AcceptancesAsync(document, take, ct));

    [HttpGet("acceptances.csv")]
    public async Task<IActionResult> AcceptancesCsv(CancellationToken ct)
    {
        var csv = await _legal.AcceptancesCsvAsync(ct);
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"aceptaciones-legales-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}

internal static class LegalEvidence
{
    public static string? Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString();

    public static string? UserAgent(HttpContext context) => context.Request.Headers.UserAgent.ToString();
}
