using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Infrastructure;

/// <summary>
/// Sends theory-class reservation-open and reminder notifications on a schedule.
/// </summary>
public sealed class TheoryReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TheoryReminderService> _logger;

    public TheoryReminderService(
        IServiceScopeFactory scopes,
        ILogger<TheoryReminderService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Theory reminder service started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Theory reminder tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TheoryTrainingService>();
        await service.ProcessRemindersAsync(ct);
    }
}
