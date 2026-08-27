using Cale.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly CaleDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        CaleDbContext db,
        IWebHostEnvironment env,
        ILogger<HealthController> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    /// <summary>Liveness: process is up.</summary>
    [HttpGet]
    [HttpGet("live")]
    [AllowAnonymous]
    public IActionResult Live() =>
        Ok(new { status = "ok", check = "live" });

    /// <summary>Readiness: can accept traffic (DB + uploads path).</summary>
    [HttpGet("ready")]
    [AllowAnonymous]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var errors = new List<string>();

        try
        {
            if (!await _db.Database.CanConnectAsync(ct))
            {
                errors.Add("database_unreachable");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health ready DB check failed");
            errors.Add("database_error");
        }

        try
        {
            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;
            var uploads = Path.Combine(webRoot, "uploads");
            Directory.CreateDirectory(uploads);
            var probe = Path.Combine(uploads, ".health");
            await System.IO.File.WriteAllTextAsync(probe, DateTime.UtcNow.ToString("O"), ct);
            System.IO.File.Delete(probe);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health ready uploads check failed");
            errors.Add("uploads_unwritable");
        }

        if (errors.Count > 0)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { status = "degraded", check = "ready", errors });
        }

        return Ok(new { status = "ok", check = "ready" });
    }
}
