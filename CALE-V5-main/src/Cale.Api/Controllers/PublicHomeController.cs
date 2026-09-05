using System.Security.Claims;
using System.Text.Json;
using Cale.Api.Services;
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
}

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
