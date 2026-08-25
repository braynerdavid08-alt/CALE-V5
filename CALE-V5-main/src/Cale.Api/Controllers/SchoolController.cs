using Cale.Api.Extensions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly CreateSchoolMemberHandler _createMember;
    private readonly AttachSchoolMemberHandler _attachMember;
    private readonly UpdateSchoolMemberHandler _updateMember;

    public SchoolController(
        GetSchoolProfileHandler profile,
        ListSchoolPlansHandler plans,
        ManageSchoolPlanHandler managePlan,
        ListSchoolMembersHandler listMembers,
        CreateSchoolMemberHandler createMember,
        AttachSchoolMemberHandler attachMember,
        UpdateSchoolMemberHandler updateMember)
    {
        _profile = profile;
        _plans = plans;
        _managePlan = managePlan;
        _listMembers = listMembers;
        _createMember = createMember;
        _attachMember = attachMember;
        _updateMember = updateMember;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<SchoolProfileDto>> Profile(
        CancellationToken ct) =>
        Ok(await _profile.HandleAsync(CurrentUser.GetId(User), ct));

    [HttpGet("plans")]
    public ActionResult<IReadOnlyList<SchoolPlanDto>> Plans() =>
        Ok(_plans.Handle());

    [HttpPut("plan")]
    public async Task<ActionResult<SchoolProfileDto>> SelectPlan(
        ChangeSchoolPlanRequest request,
        CancellationToken ct) =>
        Ok(await _managePlan.SelectPlanAsync(
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("plan/activate")]
    public async Task<ActionResult<SchoolProfileDto>> ActivatePlan(
        ActivateSchoolPlanRequest? request,
        CancellationToken ct) =>
        Ok(await _managePlan.ActivateOrRenewAsync(
            CurrentUser.GetId(User),
            request ?? new ActivateSchoolPlanRequest(null),
            ct));

    [HttpPut("billing")]
    public async Task<ActionResult<SchoolProfileDto>> UpdateBilling(
        UpdateSchoolBillingRequest request,
        CancellationToken ct) =>
        Ok(await _managePlan.UpdateBillingAsync(
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> Members(
        CancellationToken ct) =>
        Ok(await _listMembers.HandleAsync(CurrentUser.GetId(User), ct));

    [HttpPost("members")]
    public async Task<ActionResult<UserListItemDto>> CreateMember(
        CreateSchoolMemberRequest request,
        CancellationToken ct) =>
        Ok(await _createMember.HandleAsync(
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("members/attach")]
    public async Task<ActionResult<UserListItemDto>> AttachMember(
        AttachSchoolMemberRequest request,
        CancellationToken ct) =>
        Ok(await _attachMember.HandleAsync(
            CurrentUser.GetId(User),
            request,
            ct));

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

    [HttpPatch("members/{id:int}/active")]
    public async Task<ActionResult<UserListItemDto>> SetMemberActive(
        int id,
        SetUserActiveRequest request,
        CancellationToken ct) =>
        Ok(await _updateMember.SetActiveAsync(
            CurrentUser.GetId(User),
            id,
            request.IsActive,
            ct));

    [HttpDelete("members/{id:int}")]
    public async Task<IActionResult> UnlinkMember(int id, CancellationToken ct)
    {
        await _updateMember.UnlinkAsync(CurrentUser.GetId(User), id, ct);
        return NoContent();
    }
}
