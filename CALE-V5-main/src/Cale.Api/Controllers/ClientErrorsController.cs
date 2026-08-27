using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/client-errors")]
public sealed class ClientErrorsController : ControllerBase
{
    private readonly ILogger<ClientErrorsController> _logger;

    public ClientErrorsController(ILogger<ClientErrorsController> logger) =>
        _logger = logger;

    public sealed class ClientErrorReport
    {
        [MaxLength(64)]
        public string? TraceId { get; set; }

        [MaxLength(200)]
        public string? Message { get; set; }

        [MaxLength(2000)]
        public string? Stack { get; set; }

        [MaxLength(500)]
        public string? Url { get; set; }

        [MaxLength(80)]
        public string? Source { get; set; }
    }

    [HttpPost]
    [AllowAnonymous]
    [RequestSizeLimit(32_000)]
    public IActionResult Report([FromBody] ClientErrorReport? report)
    {
        var traceId = string.IsNullOrWhiteSpace(report?.TraceId)
            ? HttpContext.TraceIdentifier
            : report!.TraceId!.Trim();
        var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? "-";

        _logger.LogError(
            "Client error source={Source} url={Url} message={Message} RequestId={RequestId} UserId={UserId} stack={Stack}",
            report?.Source ?? "unknown",
            report?.Url ?? "-",
            report?.Message ?? "client_error",
            traceId,
            userId,
            Truncate(report?.Stack, 1500));

        return Accepted(new { received = true, traceId });
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value)
            ? "-"
            : value.Length <= max ? value : value[..max];
}
