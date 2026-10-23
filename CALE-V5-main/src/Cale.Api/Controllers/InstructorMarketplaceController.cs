using Cale.Api.Extensions;
using Cale.Api.Services.Instructors;
using Cale.BuildingBlocks.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cale.Api.Controllers;

/// <summary>Public reviews and signed-in ratings of instructors listed in the directory.</summary>
[ApiController]
[Route("api/instructors/{instructorId:int}")]
public sealed class InstructorRatingsController : ControllerBase
{
    private readonly InstructorMarketplaceService _market;

    public InstructorRatingsController(InstructorMarketplaceService market) => _market = market;

    [HttpGet("reviews")]
    [AllowAnonymous]
    public Task<IReadOnlyList<InstructorReviewDto>> Reviews(int instructorId, CancellationToken ct) =>
        _market.ListReviewsAsync(instructorId, CurrentUser.IsAdmin(User), ct);

    [HttpGet("rating/mine")]
    [Authorize]
    public async Task<ActionResult<MyInstructorRatingDto?>> Mine(int instructorId, CancellationToken ct) =>
        Ok(await _market.GetMyRatingAsync(CurrentUser.GetId(User), instructorId, ct));

    [HttpPut("rating")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.SchoolLinks)]
    public Task<MyInstructorRatingDto> Rate(int instructorId, RateInstructorRequest request, CancellationToken ct) =>
        _market.RateAsync(CurrentUser.GetId(User), instructorId, request, ct);
}

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/instructor-reviews")]
public sealed class AdminInstructorReviewsController : ControllerBase
{
    private readonly InstructorMarketplaceService _market;

    public AdminInstructorReviewsController(InstructorMarketplaceService market) => _market = market;

    [HttpPost("{ratingId:int}/hide")]
    public async Task<IActionResult> Hide(int ratingId, CancellationToken ct)
    {
        await _market.SetReviewHiddenAsync(ratingId, true, ct);
        return NoContent();
    }

    [HttpPost("{ratingId:int}/show")]
    public async Task<IActionResult> Show(int ratingId, CancellationToken ct)
    {
        await _market.SetReviewHiddenAsync(ratingId, false, ct);
        return NoContent();
    }
}

/// <summary>Students ask listed instructors for private classes (no payment inside the app).</summary>
[ApiController]
[Authorize(Policy = "StudentOnly")]
[Route("api/private-classes")]
public sealed class StudentPrivateClassesController : ControllerBase
{
    private readonly InstructorMarketplaceService _market;

    public StudentPrivateClassesController(InstructorMarketplaceService market) => _market = market;

    [HttpGet("mine")]
    public Task<IReadOnlyList<PrivateClassRequestDto>> Mine(CancellationToken ct) =>
        _market.ListForStudentAsync(CurrentUser.GetId(User), ct);

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.SchoolLinks)]
    public Task<PrivateClassRequestDto> Create(CreatePrivateClassRequest request, CancellationToken ct) =>
        _market.RequestClassAsync(CurrentUser.GetId(User), request, ct);

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _market.CancelAsync(CurrentUser.GetId(User), id, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/teacher/private-classes")]
public sealed class TeacherPrivateClassesController : ControllerBase
{
    private readonly InstructorMarketplaceService _market;

    public TeacherPrivateClassesController(InstructorMarketplaceService market) => _market = market;

    [HttpGet]
    public Task<IReadOnlyList<PrivateClassRequestDto>> List(CancellationToken ct) =>
        _market.ListForInstructorAsync(CurrentUser.GetId(User), ct);

    [HttpPost("{id:int}/respond")]
    public Task<PrivateClassRequestDto> Respond(int id, RespondPrivateClassRequest request, CancellationToken ct) =>
        _market.RespondAsync(CurrentUser.GetId(User), id, request, ct);

    [HttpGet("settings")]
    public Task<PrivateClassSettingsDto> Settings(CancellationToken ct) =>
        _market.GetSettingsAsync(CurrentUser.GetId(User), ct);

    [HttpPut("settings")]
    public Task<PrivateClassSettingsDto> SaveSettings([FromBody] PrivateClassSettingsDto body, CancellationToken ct) =>
        _market.SetAcceptsAsync(CurrentUser.GetId(User), body.AcceptsPrivateClasses, ct);
}
