using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Infrastructure;

/// <summary>Pushes high-priority school ops alerts once per day (bot-first, no LLM).</summary>
public sealed class SchoolOpsAlertBot : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<SchoolOpsAlertBot> _logger;

    public SchoolOpsAlertBot(
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<SchoolOpsAlertBot> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("FormationOps:EnableSchoolOpsAlertBot", true))
        {
            _logger.LogInformation("SchoolOpsAlertBot disabled.");
            return;
        }

        var from = _config.GetValue("FormationOps:OpsAlertFromHour", 7);
        var to = _config.GetValue("FormationOps:OpsAlertToHour", 9);
        var lastRunDay = DateOnly.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var local = DateTime.UtcNow.AddHours(-5);
                var today = DateOnly.FromDateTime(local);
                if (lastRunDay != today && local.Hour >= from && local.Hour < to)
                {
                    using var scope = _scopes.CreateScope();
                    var runner = scope.ServiceProvider.GetRequiredService<FormationOpsBotRunner>();
                    var count = await runner.RunSchoolOpsAlertsAsync(stoppingToken);
                    lastRunDay = today;
                    _logger.LogInformation("SchoolOpsAlertBot sent {Count} alert(s).", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SchoolOpsAlertBot tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(20), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
