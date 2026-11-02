using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Infrastructure;

/// <summary>Daily student formation nudges (bot-first, no LLM).</summary>
public sealed class FormationNudgeBot : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<FormationNudgeBot> _logger;

    public FormationNudgeBot(
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<FormationNudgeBot> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("FormationOps:EnableFormationNudgeBot", true))
        {
            _logger.LogInformation("FormationNudgeBot disabled.");
            return;
        }

        var from = _config.GetValue("FormationOps:NudgeFromHour", 17);
        var to = _config.GetValue("FormationOps:NudgeToHour", 20);
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
                    var count = await runner.RunFormationNudgesAsync(stoppingToken);
                    lastRunDay = today;
                    _logger.LogInformation("FormationNudgeBot sent {Count} nudge(s).", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FormationNudgeBot tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
