using Cale.Api.Extensions;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.WhatsApp;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolOnly")]
[Route("api/school/scheduling-reports")]
public sealed class SchedulingReportsController : ControllerBase
{
    private readonly SchedulingReportGenerator _generator;
    private readonly IWhatsAppCloudSender _whatsApp;
    private readonly SchedulingReportsOptions _options;

    public SchedulingReportsController(
        SchedulingReportGenerator generator,
        IWhatsAppCloudSender whatsApp,
        IOptions<SchedulingReportsOptions> options)
    {
        _generator = generator;
        _whatsApp = whatsApp;
        _options = options.Value;
    }

    private int SchoolId => CurrentUser.GetId(User);

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            moduleLabel = "Reportes de programación",
            activationMessage = "Pendiente de activación",
            pendingCopy =
                "Los envíos automáticos y WhatsApp están preparados pero aún no activos. Puedes descargar el Excel del día manualmente.",
            enabled = _options.Enabled,
            allowManualDownload = _options.AllowManualDownload,
            allowManualWhatsAppSend = _options.CanManualWhatsAppSend,
            enableDailyFullReportJob = _options.EnableDailyFullReportJob,
            enableDailyChangesReportJob = _options.EnableDailyChangesReportJob,
            whatsAppConfigured = _options.WhatsApp.IsConfigured,
            dailyJobsArmed = _options.DailyJobsArmed,
            scheduleInfo = new
            {
                fullReportLocal = "14:00 America/Bogota",
                changesReportLocal = "17:00 America/Bogota",
                note = "Los horarios están documentados; no hay job ni Cron activo en esta fase."
            }
        });
    }

    [HttpPost("generate/today")]
    public async Task<IActionResult> GenerateToday(CancellationToken ct)
    {
        if (!_options.CanManualDownload)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "La descarga manual de reportes de programación no está habilitada."
            });
        }

        var today = ColombiaTime.TodayInColombia();
        var file = await _generator.GenerateDayAsync(
            SchoolId,
            today,
            SchedulingReportTypes.ManualToday,
            ct);
        return File(
            file.Bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.FileName);
    }

    [HttpPost("generate/tomorrow")]
    public async Task<IActionResult> GenerateTomorrow(CancellationToken ct)
    {
        if (!_options.CanManualDownload)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "La descarga manual de reportes de programación no está habilitada."
            });
        }

        var tomorrow = ColombiaTime.TodayInColombia().AddDays(1);
        var file = await _generator.GenerateDayAsync(
            SchoolId,
            tomorrow,
            SchedulingReportTypes.ManualTomorrow,
            ct);
        return File(
            file.Bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.FileName);
    }

    [HttpPost("send/today")]
    public IActionResult SendToday() => WhatsAppBlocked();

    [HttpPost("send/tomorrow")]
    public IActionResult SendTomorrow() => WhatsAppBlocked();

    private IActionResult WhatsAppBlocked()
    {
        // Call stub so tests/logs prove Graph is never hit; still reject at the API gate.
        _ = _whatsApp;
        if (!_options.CanManualWhatsAppSend)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Envío por WhatsApp pendiente de activación. Luz Verde aún no ha habilitado la integración.",
                status = "NotConfigured"
            });
        }

        return BadRequest(new
        {
            error = "El envío por WhatsApp no está disponible en esta fase.",
            status = "NotConfigured"
        });
    }
}
