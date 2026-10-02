using System.Globalization;
using Cale.Api.Services.Play;

namespace Cale.Api.Services.Admin;

/// <summary>
/// Sends admins the weekly usage summary on Monday mornings (Colombia time, 8–12 h).
/// Disable with Admin:WeeklySummary=false.
/// </summary>
public sealed class WeeklySummaryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<WeeklySummaryService> _logger;

    public WeeklySummaryService(
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<WeeklySummaryService> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Admin:WeeklySummary", true))
        {
            return;
        }

        string? lastWeek = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var local = DateTime.UtcNow.AddHours(PlayService.ColombiaUtcOffsetHours);
                var week = $"{ISOWeek.GetYear(local)}-W{ISOWeek.GetWeekOfYear(local):00}";
                if (week != lastWeek && local.DayOfWeek == DayOfWeek.Monday && local.Hour is >= 8 and < 12)
                {
                    using var scope = _scopes.CreateScope();
                    var insights = scope.ServiceProvider.GetRequiredService<AdminInsightsService>();
                    var sent = await insights.SendWeeklySummaryAsync(week, stoppingToken);
                    lastWeek = week;
                    _logger.LogInformation("Weekly admin summary {Week} sent to {Count} admin(s).", week, sent);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weekly admin summary tick failed");
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
