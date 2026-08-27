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

    public AdminMembershipsController(
        ManageSchoolPlanHandler managePlan,
        PilotMetricsService metrics)
    {
        _managePlan = managePlan;
        _metrics = metrics;
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
        CancellationToken ct) =>
        Ok(await _managePlan.AdminActivateAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("memberships/{schoolUserId:int}/reject")]
    public async Task<ActionResult<SchoolProfileDto>> Reject(
        int schoolUserId,
        RejectSchoolMembershipRequest? request,
        CancellationToken ct) =>
        Ok(await _managePlan.AdminRejectAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct));

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
        CancellationToken ct) =>
        Ok(await _managePlan.AdminSuspendAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("memberships/{schoolUserId:int}/unsuspend")]
    public async Task<ActionResult<SchoolProfileDto>> Unsuspend(
        int schoolUserId,
        CancellationToken ct) =>
        Ok(await _managePlan.AdminUnsuspendAsync(
            schoolUserId,
            CurrentUser.GetId(User),
            ct));

    [HttpGet("metrics")]
    public async Task<ActionResult<PilotMetricsDto>> Metrics(CancellationToken ct) =>
        Ok(await _metrics.GetAsync(ct));
}
