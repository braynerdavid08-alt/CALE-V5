using Cale.Api.Extensions;
using Cale.Api.Infrastructure;
using Cale.Api.Services;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolOnly")]
[Route("api/school")]
public sealed class SchoolController : ControllerBase
{
    private readonly GetSchoolProfileHandler _profile;
    private readonly ListSchoolPlansHandler _plans;
    private readonly ManageSchoolPlanHandler _managePlan;
    private readonly ListSchoolMembersHandler _listMembers;
    private readonly UpdateSchoolMemberHandler _updateMember;
    private readonly SchoolJoinRequestHandler _joinRequests;

    public SchoolController(
        GetSchoolProfileHandler profile,
        ListSchoolPlansHandler plans,
        ManageSchoolPlanHandler managePlan,
        ListSchoolMembersHandler listMembers,
        UpdateSchoolMemberHandler updateMember,
        SchoolJoinRequestHandler joinRequests)
    {
        _profile = profile;
        _plans = plans;
        _managePlan = managePlan;
        _listMembers = listMembers;
        _updateMember = updateMember;
        _joinRequests = joinRequests;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<SchoolProfileDto>> Profile(
        CancellationToken ct) =>
        Ok(await _profile.HandleAsync(CurrentUser.GetId(User), ct));

    [HttpGet("plans")]
    public ActionResult<IReadOnlyList<SchoolPlanDto>> Plans() =>
        Ok(_plans.Handle());

    /// <summary>
    /// School requests a membership plan. Admin must verify payment and activate.
    /// </summary>
    [HttpPost("plan/request")]
    public async Task<ActionResult<SchoolProfileDto>> RequestPlan(
        RequestSchoolMembershipRequest? request,
        CancellationToken ct) =>
        Ok(await _managePlan.RequestMembershipAsync(
            CurrentUser.GetId(User),
            request ?? new RequestSchoolMembershipRequest(null),
            ct));

    [HttpPost("plan/proof")]
    public async Task<ActionResult<SchoolProfileDto>> SubmitProof(
        SubmitPaymentProofRequest request,
        CancellationToken ct) =>
        Ok(await _managePlan.SubmitPaymentProofAsync(
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("plan/cancel")]
    public async Task<ActionResult<SchoolProfileDto>> CancelPlan(
        CancelSchoolMembershipRequest? request,
        CancellationToken ct) =>
        Ok(await _managePlan.CancelRequestAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("plan/proof/upload")]
    [EnableRateLimiting(RateLimitPolicies.Uploads)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<object>> UploadProof(
        [FromForm] IFormFile? file,
        [FromServices] IWebHostEnvironment env,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Selecciona el comprobante.",
                Detail = "invalid_file",
                Status = 400
            });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "El archivo debe pesar 5 MB o menos.",
                Detail = "file_too_large",
                Status = 400
            });
        }

        var detected = await MediaSniffer.DetectAsync(file, ct);
        if (detected is not { Kind: "image" or "document" })
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Usa jpg, png, webp o pdf.",
                Detail = "invalid_file",
                Status = 400
            });
        }

        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;
        var folder = Path.Combine(webRoot, "uploads", "receipts");
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{detected.Extension}";
        var path = Path.Combine(folder, name);
        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream, ct);
        return Ok(new { url = $"/uploads/receipts/{name}" });
    }

    [HttpGet("plan/history")]
    public async Task<ActionResult<IReadOnlyList<MembershipEventDto>>> History(
        CancellationToken ct) =>
        Ok(await _managePlan.ListHistoryAsync(CurrentUser.GetId(User), ct));

    [HttpPut("billing")]
    public async Task<ActionResult<SchoolProfileDto>> UpdateBilling(
        UpdateSchoolBillingRequest request,
        [FromServices] HomepageService homepage,
        CancellationToken ct)
    {
        var dto = await _managePlan.UpdateBillingAsync(CurrentUser.GetId(User), request, ct);
        homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    /// <summary>
    /// Schools cannot activate their own membership.
    /// </summary>
    [HttpPut("plan")]
    [HttpPost("plan/activate")]
    public IActionResult MembershipActivationForbidden() =>
        StatusCode(
            StatusCodes.Status403Forbidden,
            new ProblemDetails
            {
                Title = "Solo el administrador puede activar o actualizar la membresía tras verificar el pago.",
                Detail = "membership_admin_only",
                Status = StatusCodes.Status403Forbidden
            });

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> Members(
        CancellationToken ct) =>
        Ok(await _listMembers.HandleAsync(CurrentUser.GetId(User), ct));

    [HttpGet("join-requests")]
    public async Task<ActionResult<IReadOnlyList<SchoolJoinRequestDto>>> JoinRequests(
        CancellationToken ct) =>
        Ok(await _joinRequests.ListPendingForSchoolAsync(CurrentUser.GetId(User), ct));

    [HttpPost("join-requests/{id:int}/accept")]
    public async Task<ActionResult<SchoolJoinRequestDto>> AcceptJoin(
        int id,
        CancellationToken ct) =>
        Ok(await _joinRequests.AcceptAsync(CurrentUser.GetId(User), id, ct));

    [HttpPost("join-requests/{id:int}/reject")]
    public async Task<ActionResult<SchoolJoinRequestDto>> RejectJoin(
        int id,
        [FromBody] RejectSchoolJoinRequest? body,
        CancellationToken ct) =>
        Ok(await _joinRequests.RejectAsync(
            CurrentUser.GetId(User),
            id,
            body,
            ct));

    [HttpPost("invitations")]
    [EnableRateLimiting(RateLimitPolicies.SchoolLinks)]
    public async Task<ActionResult<SchoolInviteResultDto>> Invite(
        InviteSchoolMemberRequest request,
        CancellationToken ct) =>
        Ok(await _joinRequests.InviteAsync(CurrentUser.GetId(User), request, ct));

    [HttpPost("invitations/{id:int}/cancel")]
    public async Task<IActionResult> CancelInvite(
        int id,
        CancellationToken ct)
    {
        await _joinRequests.CancelInviteAsync(CurrentUser.GetId(User), id, ct);
        return NoContent();
    }

    [HttpPut("members/{id:int}")]
    public async Task<ActionResult<UserListItemDto>> UpdateMember(
        int id,
        UpdateSchoolMemberRequest request,
        CancellationToken ct) =>
        Ok(await _updateMember.HandleAsync(
            CurrentUser.GetId(User),
            id,
            request,
            ct));

    // Activar/desactivar y quitar miembros: solo administrador (Usuarios / Escuelas).
}
