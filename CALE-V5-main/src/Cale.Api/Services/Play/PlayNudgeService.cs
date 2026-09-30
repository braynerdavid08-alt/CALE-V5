namespace Cale.Api.Services.Play;

/// <summary>
/// Sends the daily playful practice reminder to inactive students during the Colombia evening window
/// (Play:NudgeFromHour..Play:NudgeToHour, default 18–21). Dedupe keys keep it to one per student per day.
/// </summary>
public sealed class PlayNudgeService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<PlayNudgeService> _logger;

    public PlayNudgeService(
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<PlayNudgeService> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Play:AutoNudges", true))
        {
            _logger.LogInformation("Play auto nudges disabled (Play:AutoNudges=false).");
            return;
        }

        var from = _config.GetValue("Play:NudgeFromHour", 18);
        var to = _config.GetValue("Play:NudgeToHour", 21);
        var lastRunDay = DateOnly.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var local = DateTime.UtcNow.AddHours(PlayService.ColombiaUtcOffsetHours);
                var today = DateOnly.FromDateTime(local);
                if (lastRunDay != today && local.Hour >= from && local.Hour < to)
                {
                    using var scope = _scopes.CreateScope();
                    var play = scope.ServiceProvider.GetRequiredService<PlayService>();
                    var count = await play.SendAutoNudgesAsync(stoppingToken);
                    lastRunDay = today;
                    _logger.LogInformation("Play auto nudges processed for {Count} inactive student(s).", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Play auto nudge tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
