namespace Cale.Api.Services.Tournaments;

/// <summary>Periodically resolves question timeouts and disconnect walkovers.</summary>
public sealed class TournamentTimeoutWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TournamentTimeoutWorker> _logger;

    public TournamentTimeoutWorker(IServiceScopeFactory scopes, ILogger<TournamentTimeoutWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                using var scope = _scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<TournamentService>();
                await service.ProcessTimeoutsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TournamentTimeoutWorker tick failed");
            }
        }
    }
}
