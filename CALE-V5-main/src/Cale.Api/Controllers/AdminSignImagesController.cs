using Cale.Api.Services.Play;
using Cale.BuildingBlocks.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

/// <summary>Which catalog signs got a real picture from the sign exams, and which exam questions did not match.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/signal-images")]
public sealed class AdminSignImagesController : ControllerBase
{
    private readonly SignImageOverrides _overrides;

    public AdminSignImagesController(SignImageOverrides overrides) => _overrides = overrides;

    [HttpGet]
    public async Task<SignImageReport> Get([FromQuery] bool refresh, CancellationToken ct) =>
        await _overrides.ReportAsync(refresh, ct);
}
