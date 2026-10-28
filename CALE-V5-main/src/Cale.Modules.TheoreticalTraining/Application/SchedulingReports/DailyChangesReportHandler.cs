using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cale.Modules.TheoreticalTraining.Application.SchedulingReports;

/// <summary>
/// Invocable handler for the future 17:00 Colombia changes report.
/// Not registered as a HostedService while EnableDailyChangesReportJob is false.
/// </summary>
public sealed class DailyChangesReportHandler
{
    private readonly SchedulingReportGenerator _generator;
    private readonly SchedulingReportsOptions _options;
    private readonly ILogger<DailyChangesReportHandler> _logger;

    public DailyChangesReportHandler(
        SchedulingReportGenerator generator,
        IOptions<SchedulingReportsOptions> options,
        ILogger<DailyChangesReportHandler> logger)
    {
        _generator = generator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunForSchoolAsync(int schoolUserId, DateOnly targetDate, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.EnableDailyChangesReportJob)
        {
            _logger.LogInformation(
                "DailyChangesReport skipped for school {SchoolId}: jobs not armed.",
                schoolUserId);
            return;
        }

        var (file, diff) = await _generator.TryGenerateChangesAsync(schoolUserId, targetDate, ct);
        if (file is null)
        {
            _logger.LogInformation(
                "DailyChangesReport: no changes for school {SchoolId} date {Date}; no file.",
                schoolUserId,
                targetDate);
            return;
        }

        _logger.LogInformation(
            "DailyChangesReport generated for school {SchoolId} date {Date} changes {Count} (send not performed).",
            schoolUserId,
            targetDate,
            diff.Changes.Count);
    }
}
