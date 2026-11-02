using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Infrastructure;

/// <summary>Practical lesson / exam reminders (bot-first, no LLM).</summary>
public sealed class PracticalReminderBot : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<PracticalReminderBot> _logger;

    public PracticalReminderBot(
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<PracticalReminderBot> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("FormationOps:EnablePracticalReminderBot", true))
        {
            _logger.LogInformation("PracticalReminderBot disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<FormationOpsBotRunner>();
                var count = await runner.RunPracticalRemindersAsync(stoppingToken);
                if (count > 0)
                {
                    _logger.LogInformation("PracticalReminderBot sent {Count} reminder(s).", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PracticalReminderBot tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
