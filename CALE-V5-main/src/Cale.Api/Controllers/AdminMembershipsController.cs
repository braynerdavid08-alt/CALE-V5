using Cale.Api.Extensions;
using Cale.Api.Services;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin")]
public sealed class AdminMembershipsController : ControllerBase
{
    private readonly ManageSchoolPlanHandler _managePlan;
    private readonly PilotMetricsService _metrics;
    private readonly HomepageService _homepage;
    private readonly Cale.Api.Services.Admin.SchoolDeletionService _schoolDeletion;

    public AdminMembershipsController(
        ManageSchoolPlanHandler managePlan,
        PilotMetricsService metrics,
        HomepageService homepage,
        Cale.Api.Services.Admin.SchoolDeletionService schoolDeletion)
    {
        _managePlan = managePlan;
        _metrics = metrics;
        _homepage = homepage;
        _schoolDeletion = schoolDeletion;
    }

    /// <summary>Deletes a school with no linked users or content (409 school_not_empty otherwise).</summary>
    [HttpDelete("schools/{schoolUserId:int}")]
    public async Task<IActionResult> DeleteSchool(int schoolUserId, CancellationToken ct)
    {
        await _schoolDeletion.DeleteAsync(CurrentUser.GetId(User), schoolUserId, ct);
        return NoContent();
    }

    [HttpGet("memberships/pending")]
    public async Task<ActionResult<IReadOnlyList<SchoolMembershipRequestDto>>> Pending(
        CancellationToken ct) =>
        Ok(await _managePlan.ListPendingAsync(ct));

    [HttpGet("schools")]
    public async Task<ActionResult<IReadOnlyList<AdminSchoolSummaryDto>>> Schools(
        CancellationToken ct) =>
        Ok(await _managePlan.ListSchoolsAsync(ct));

    [HttpGet("schools/{schoolUserId:int}")]
    public async Task<ActionResult<AdminSchoolDetailDto>> SchoolDetail(
        int schoolUserId,
        CancellationToken ct) =>
        Ok(await _managePlan.GetSchoolDetailAsync(schoolUserId, ct));

    [HttpGet("schools/{schoolUserId:int}/history")]
    public async Task<ActionResult<IReadOnlyList<MembershipEventDto>>> SchoolHistory(
        int schoolUserId,
        CancellationToken ct) =>
        Ok(await _managePlan.ListHistoryAsync(schoolUserId, ct));

    [HttpPut("schools/{schoolUserId:int}/billing")]
    public async Task<ActionResult<SchoolProfileDto>> UpdateBilling(
        int schoolUserId,
        UpdateSchoolBillingRequest request,
        CancellationToken ct)
    {
        var dto = await _managePlan.UpdateBillingAsync(schoolUserId, request, ct);
        _homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    [HttpPut("schools/{schoolUserId:int}/seats")]
    public async Task<ActionResult<SchoolProfileDto>> SetSeats(
        int schoolUserId,
        AdminSetSchoolSeatsRequest request,
        CancellationToken ct) =>
        Ok(await _managePlan.AdminSetSeatsAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPut("schools/{schoolUserId:int}/membership")]
    public async Task<ActionResult<SchoolProfileDto>> OverrideMembership(
        int schoolUserId,
        AdminOverrideSchoolMembershipRequest request,
        CancellationToken ct) =>
        Ok(await _managePlan.AdminOverrideMembershipAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("schools/{schoolUserId:int}/reopen")]
    public async Task<ActionResult<SchoolProfileDto>> Reopen(
        int schoolUserId,
        AdminReopenSchoolRequest? request,
        CancellationToken ct) =>
        Ok(await _managePlan.AdminReopenAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("memberships/{schoolUserId:int}/activate")]
    public async Task<ActionResult<SchoolProfileDto>> Activate(
        int schoolUserId,
        ActivateSchoolPlanRequest? request,
        CancellationToken ct)
    {
        var dto = await _managePlan.AdminActivateAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct);
        _homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    [HttpPost("memberships/{schoolUserId:int}/reject")]
    public async Task<ActionResult<SchoolProfileDto>> Reject(
        int schoolUserId,
        RejectSchoolMembershipRequest? request,
        CancellationToken ct)
    {
        var dto = await _managePlan.AdminRejectAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct);
        _homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    [HttpPost("memberships/{schoolUserId:int}/cancel")]
    public async Task<ActionResult<SchoolProfileDto>> Cancel(
        int schoolUserId,
        CancelSchoolMembershipRequest? request,
        CancellationToken ct) =>
        Ok(await _managePlan.CancelRequestAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("memberships/{schoolUserId:int}/suspend")]
    public async Task<ActionResult<SchoolProfileDto>> Suspend(
        int schoolUserId,
        SuspendSchoolMembershipRequest? request,
        CancellationToken ct)
    {
        var dto = await _managePlan.AdminSuspendAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct);
        _homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    [HttpPost("schools/{schoolUserId:int}/approve")]
    public async Task<ActionResult<SchoolProfileDto>> Approve(
        int schoolUserId,
        CancellationToken ct)
    {
        var dto = await _managePlan.AdminApproveAsync(schoolUserId, CurrentUser.GetId(User), ct);
        _homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    [HttpPost("memberships/{schoolUserId:int}/unsuspend")]
    public async Task<ActionResult<SchoolProfileDto>> Unsuspend(
        int schoolUserId,
        CancellationToken ct)
    {
        var dto = await _managePlan.AdminUnsuspendAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            ct);
        _homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    [HttpGet("commercial-plans")]
    public async Task<ActionResult<IReadOnlyList<SchoolPlanDto>>> CommercialPlans(
        [FromQuery] bool includeInactive = true,
        CancellationToken ct = default) =>
        Ok(await _managePlan.ListCommercialPlansAsync(includeInactive, ct));

    [HttpPut("commercial-plans/{code}")]
    public async Task<ActionResult<SchoolPlanDto>> UpdateCommercialPlan(
        string code,
        UpdateCommercialPlanRequest request,
        CancellationToken ct) =>
        Ok(await _managePlan.UpdateCommercialPlanAsync(
            code,
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("schools/{schoolUserId:int}/subscription")]
    public async Task<ActionResult<SchoolProfileDto>> AssignSubscription(
        int schoolUserId,
        AdminAssignSubscriptionRequest request,
        CancellationToken ct)
    {
        var dto = await _managePlan.AdminAssignSubscriptionAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct);
        _homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    [HttpPut("schools/{schoolUserId:int}/payment-status")]
    public async Task<ActionResult<SchoolProfileDto>> SetPaymentStatus(
        int schoolUserId,
        AdminSetPaymentStatusRequest request,
        CancellationToken ct) =>
        Ok(await _managePlan.AdminSetPaymentStatusAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("schools/{schoolUserId:int}/courtesy")]
    public async Task<ActionResult<SchoolProfileDto>> GrantCourtesy(
        int schoolUserId,
        AdminCourtesyRequest request,
        CancellationToken ct)
    {
        var dto = await _managePlan.AdminGrantCourtesyAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct);
        _homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    [HttpDelete("schools/{schoolUserId:int}/courtesy")]
    public async Task<ActionResult<SchoolProfileDto>> ClearCourtesy(
        int schoolUserId,
        AdminClearCourtesyRequest? request,
        CancellationToken ct)
    {
        var dto = await _managePlan.AdminClearCourtesyAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct);
        _homepage.InvalidatePublicCache();
        return Ok(dto);
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<PilotMetricsDto>> Metrics(CancellationToken ct) =>
        Ok(await _metrics.GetAsync(ct));
}
