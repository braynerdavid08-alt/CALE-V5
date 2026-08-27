using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.LiveClassroom.Application.Abstractions;
using Cale.Modules.LiveClassroom.Application.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.LiveClassroom.Infrastructure;

/// <summary>
/// Closes live questions when QuestionClosesAt elapses (server-side timer).
/// </summary>
public sealed class LiveQuestionTimerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LiveQuestionTimerService> _logger;

    public LiveQuestionTimerService(
        IServiceScopeFactory scopes,
        ILogger<LiveQuestionTimerService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Live question timer service started");
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
                _logger.LogError(ex, "Live question timer tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
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
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var store = scope.ServiceProvider.GetRequiredService<ILiveSessionStore>();
        var handler = scope.ServiceProvider.GetRequiredService<LiveSessionHandler>();

        var ids = await store.ListExpiredOpenSessionIdsAsync(clock.UtcNow, ct);
        foreach (var id in ids)
        {
            try
            {
                await handler.AutoCloseExpiredAsync(id, ct);
                _logger.LogInformation("Auto-closed expired live question for session {SessionId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-close session {SessionId}", id);
            }
        }
    }
}
