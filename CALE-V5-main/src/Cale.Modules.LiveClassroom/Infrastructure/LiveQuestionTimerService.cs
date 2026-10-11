using System.Diagnostics;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.LiveClassroom.Application.Abstractions;
using Cale.Modules.LiveClassroom.Application.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.LiveClassroom.Infrastructure;

/// <summary>
/// Closes live questions when QuestionClosesAt elapses (server-side timer).
/// Sleeps until the next pending deadline (capped by <see cref="IdleDelay"/>) and is woken by
/// <see cref="LiveQuestionTimerSignal"/> when a question opens in this process, so an idle
/// server does not query the database every second.
/// </summary>
public sealed class LiveQuestionTimerService : BackgroundService
{
    public static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan MaxErrorDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SummaryEvery = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxSessionBackoff = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BackoffFloor = TimeSpan.FromSeconds(5);
    private const int MaxBatch = 40;

    private readonly IServiceScopeFactory _scopes;
    private readonly LiveQuestionTimerSignal _signal;
    private readonly ILogger<LiveQuestionTimerService> _logger;
    private readonly Dictionary<int, (int Failures, DateTime RetryAt)> _failingSessions = [];
    private TimerStats _stats = new();
    private DateTime _statsSince = DateTime.UtcNow;

    public LiveQuestionTimerService(
        IServiceScopeFactory scopes,
        LiveQuestionTimerSignal signal,
        ILogger<LiveQuestionTimerService> logger)
    {
        _scopes = scopes;
        _signal = signal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Live question timer service started");
        var consecutiveErrors = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                delay = await TickAsync(stoppingToken);
                consecutiveErrors = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                _stats.Errors++;
                delay = ErrorDelay(consecutiveErrors);
                _logger.LogError(
                    ex,
                    "Live question timer tick failed ({ConsecutiveErrors} in a row); retrying in {DelaySeconds}s",
                    consecutiveErrors,
                    delay.TotalSeconds);
            }

            MaybeLogSummary();

            try
            {
                await WaitAsync(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public static TimeSpan ErrorDelay(int consecutiveErrors)
    {
        var seconds = Math.Pow(2, Math.Clamp(consecutiveErrors, 1, 10));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxErrorDelay.TotalSeconds));
    }

    /// <summary>Delay until the next deadline, clamped to [MinDelay, IdleDelay].</summary>
    public static TimeSpan NextDelay(DateTime? nextDeadline, DateTime utcNow)
    {
        if (nextDeadline is null)
        {
            return IdleDelay;
        }

        var wait = nextDeadline.Value - utcNow;
        if (wait < MinDelay)
        {
            return MinDelay;
        }

        return wait > IdleDelay ? IdleDelay : wait;
    }

    private async Task WaitAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(delay);
        try
        {
            await _signal.WaitAsync(timeout.Token);
            _stats.Wakeups++;
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task<TimeSpan> TickAsync(CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        _stats.Ticks++;

        using var scope = _scopes.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var store = scope.ServiceProvider.GetRequiredService<ILiveSessionStore>();
        var handler = scope.ServiceProvider.GetRequiredService<LiveSessionHandler>();

        var now = clock.UtcNow;
        var ids = await store.ListExpiredOpenSessionIdsAsync(now, ct);
        _stats.Detected += ids.Count;
        var closedNow = 0;
        foreach (var id in ids)
        {
            _failingSessions.TryGetValue(id, out var failing);
            if (failing.RetryAt > now)
            {
                continue;
            }

            _stats.Attempts++;
            try
            {
                if (await handler.AutoCloseExpiredAsync(id, ct))
                {
                    closedNow++;
                    _stats.Closed++;
                    _failingSessions.Remove(id);
                    _logger.LogInformation("Auto-closed expired live question for session {SessionId}", id);
                }
                else
                {
                    // Still selected but not closable: back off instead of re-reading it every tick.
                    _stats.AlreadyClosed++;
                    BackOff(id, failing.Failures, now);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _stats.Errors++;
                var backoff = BackOff(id, failing.Failures, now);
                _logger.LogWarning(
                    ex,
                    "Failed to auto-close session {SessionId} (attempt {Failures}); next retry in {BackoffSeconds}s",
                    id,
                    failing.Failures + 1,
                    backoff.TotalSeconds);
            }
        }

        if (ids.Count < MaxBatch)
        {
            foreach (var stale in _failingSessions.Keys.Where(k => !ids.Contains(k)).ToList())
            {
                _failingSessions.Remove(stale);
            }
        }

        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _stats.TotalTickMs += elapsedMs;
        _stats.MaxTickMs = Math.Max(_stats.MaxTickMs, elapsedMs);

        if (closedNow > 0)
        {
            return MinDelay;
        }

        var delay = NextDelay(await store.GetNextPendingCloseAsync(ct), clock.UtcNow);
        // Only backing-off sessions are overdue: don't spin on them.
        return ids.Count > 0 && delay < BackoffFloor ? BackoffFloor : delay;
    }

    private TimeSpan BackOff(int sessionId, int previousFailures, DateTime now)
    {
        var failures = previousFailures + 1;
        var backoff = TimeSpan.FromSeconds(Math.Min(
            Math.Pow(2, Math.Clamp(failures, 1, 12)),
            MaxSessionBackoff.TotalSeconds));
        _failingSessions[sessionId] = (failures, now + backoff);
        return backoff;
    }

    private void MaybeLogSummary()
    {
        var now = DateTime.UtcNow;
        if (now - _statsSince < SummaryEvery)
        {
            return;
        }

        var s = _stats;
        if (s.Detected > 0 || s.Errors > 0)
        {
            _logger.LogInformation(
                "Live question timer summary: {Ticks} ticks, {Wakeups} wake-ups, {Detected} expired detected, {Attempts} close attempts, {Closed} closed, {AlreadyClosed} already closed, {Errors} errors, avg tick {AvgTickMs:F1} ms, max tick {MaxTickMs:F1} ms over {Minutes:F0} min",
                s.Ticks,
                s.Wakeups,
                s.Detected,
                s.Attempts,
                s.Closed,
                s.AlreadyClosed,
                s.Errors,
                s.Ticks == 0 ? 0 : s.TotalTickMs / s.Ticks,
                s.MaxTickMs,
                (now - _statsSince).TotalMinutes);
        }
        else
        {
            _logger.LogDebug(
                "Live question timer idle: {Ticks} ticks over {Minutes:F0} min",
                s.Ticks,
                (now - _statsSince).TotalMinutes);
        }

        _stats = new TimerStats();
        _statsSince = now;
    }

    private sealed class TimerStats
    {
        public long Ticks;
        public long Wakeups;
        public long Detected;
        public long Attempts;
        public long Closed;
        public long AlreadyClosed;
        public long Errors;
        public double TotalTickMs;
        public double MaxTickMs;
    }
}
