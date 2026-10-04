using System.Security.Claims;
using System.Text.Json;
using Cale.Api.Extensions;
using Cale.Api.Services;
using Cale.BuildingBlocks.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicHomeController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HomepageService _home;

    public PublicHomeController(HomepageService home) => _home = home;

    [HttpGet("home")]
    public async Task<IActionResult> Home(CancellationToken ct)
    {
        PublicHomeDto dto;
        try
        {
            dto = await _home.GetPublicHomeAsync(ct);
        }
        catch
        {
            dto = HomepageService.EmergencyHome();
        }

        try
        {
            var json = JsonSerializer.Serialize(dto, JsonOpts);
            return Content(json, "application/json; charset=utf-8");
        }
        catch
        {
            var json = JsonSerializer.Serialize(HomepageService.EmergencyHome(), JsonOpts);
            return Content(json, "application/json; charset=utf-8");
        }
    }

    [HttpGet("schools")]
    public Task<IReadOnlyList<PublicSchoolCardDto>> Schools(
        [FromQuery] int take = 24,
        CancellationToken ct = default) =>
        _home.ListPublicSchoolsAsync(take, ct);

    [HttpGet("instructors")]
    public Task<IReadOnlyList<PublicInstructorCardDto>> Instructors(
        [FromQuery] int take = 24,
        CancellationToken ct = default) =>
        _home.ListPublicInstructorsAsync(take, ct);

    [HttpGet("testimonials")]
    public Task<PublicTestimonialsDto> Testimonials(
        [FromQuery] int take = 6,
        CancellationToken ct = default) =>
        _home.ListPublicTestimonialsAsync(take, ct);
}

[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/me/directory-listing")]
public sealed class InstructorDirectoryController : ControllerBase
{
    private readonly HomepageService _home;

    public InstructorDirectoryController(HomepageService home) => _home = home;

    [HttpGet]
    public async Task<DirectoryListingDto> Get(CancellationToken ct) =>
        new(await _home.IsListedInDirectoryAsync(CurrentUser.GetId(User), ct));

    [HttpPut]
    public async Task<DirectoryListingDto> Put([FromBody] DirectoryListingDto body, CancellationToken ct) =>
        new(await _home.SetDirectoryListingAsync(CurrentUser.GetId(User), body.Listed, ct));
}

public sealed record DirectoryListingDto(bool Listed);

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/homepage")]
public sealed class AdminHomepageController : ControllerBase
{
    private readonly HomepageService _home;

    public AdminHomepageController(HomepageService home) => _home = home;

    [HttpGet]
    public Task<AdminHomepageDto> Get(CancellationToken ct) =>
        _home.GetAdminAsync(ct);

    [HttpPut]
    public Task<AdminHomepageDto> Put([FromBody] UpdateHomepageRequest body, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        return _home.SaveAdminAsync(body, userId, ct);
    }
}
