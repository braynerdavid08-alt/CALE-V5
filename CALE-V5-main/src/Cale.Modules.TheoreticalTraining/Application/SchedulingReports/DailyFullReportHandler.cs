using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cale.Modules.TheoreticalTraining.Application.SchedulingReports;

/// <summary>
/// Invocable handler for the future 14:00 Colombia full-day report.
/// Not registered as a HostedService while EnableDailyFullReportJob is false.
/// </summary>
public sealed class DailyFullReportHandler
{
    private readonly SchedulingReportGenerator _generator;
    private readonly SchedulingReportsOptions _options;
    private readonly ILogger<DailyFullReportHandler> _logger;

    public DailyFullReportHandler(
        SchedulingReportGenerator generator,
        IOptions<SchedulingReportsOptions> options,
        ILogger<DailyFullReportHandler> logger)
    {
        _generator = generator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunForSchoolAsync(int schoolUserId, DateOnly targetDate, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.EnableDailyFullReportJob)
        {
            _logger.LogInformation(
                "DailyFullReport skipped for school {SchoolId}: jobs not armed.",
                schoolUserId);
            return;
        }

        var file = await _generator.GenerateDayAsync(
            schoolUserId,
            targetDate,
            Domain.SchedulingReportTypes.DailyFull,
            ct);
        await _generator.SaveTwoPmBaselineAsync(schoolUserId, targetDate, ct);
        _logger.LogInformation(
            "DailyFullReport generated for school {SchoolId} date {Date} bytes {Bytes} (send not performed).",
            schoolUserId,
            targetDate,
            file.Bytes.Length);
    }
}
