using System.Collections.Concurrent;
using System.Reflection;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.LiveClassroom.Application.Abstractions;
using Cale.Modules.LiveClassroom.Application.Commands;
using Cale.Modules.LiveClassroom.Domain;
using Cale.Modules.LiveClassroom.Infrastructure;
using Cale.Modules.LiveClassroom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cale.UnitTests.Live;

public sealed class LiveQuestionTimerTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cale-live-{Guid.NewGuid():N}.db");
    private readonly FakeClock _clock = new() { UtcNow = T0 };
    private readonly ConcurrentBag<string> _broadcasts = [];

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }

    [Fact]
    public async Task Expired_question_is_closed_once_and_never_selected_again()
    {
        var id = await SeedRunningAsync(closesAt: T0.AddSeconds(30));
        _clock.UtcNow = T0.AddSeconds(31);

        await using (var db = NewDb())
        {
            var store = new LiveSessionStore(db);
            Assert.Equal([id], await store.ListExpiredOpenSessionIdsAsync(_clock.UtcNow));
            Assert.True(await Handler(db).AutoCloseExpiredAsync(id, default));
        }

        var closedBroadcasts = _broadcasts.Count(b => b == nameof(ILiveSessionBroadcaster.QuestionClosedAsync));
        Assert.Equal(1, closedBroadcasts);

        for (var tick = 1; tick <= 5; tick++)
        {
            _clock.UtcNow = T0.AddSeconds(31 + tick);
            await using var db = NewDb();
            var store = new LiveSessionStore(db);
            Assert.Empty(await store.ListExpiredOpenSessionIdsAsync(_clock.UtcNow));
            Assert.False(await Handler(db).AutoCloseExpiredAsync(id, default));
        }

        Assert.Equal(1, _broadcasts.Count(b => b == nameof(ILiveSessionBroadcaster.QuestionClosedAsync)));
    }

    [Fact]
    public async Task Close_state_is_persisted()
    {
        var id = await SeedRunningAsync(closesAt: T0.AddSeconds(30));
        _clock.UtcNow = T0.AddSeconds(40);
        await using (var db = NewDb())
        {
            Assert.True(await Handler(db).AutoCloseExpiredAsync(id, default));
        }

        await using var check = NewDb();
        var session = await check.Set<LiveSession>().AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Equal(T0.AddSeconds(40), session.QuestionClosedAt);
        Assert.Equal(T0.AddSeconds(40), session.QuestionClosesAt);
        Assert.Equal(LiveSessionStatuses.Running, session.Status);
        Assert.False(session.IsQuestionOpen(T0.AddSeconds(41)));
    }

    [Fact]
    public async Task Future_question_is_not_closed_early()
    {
        var id = await SeedRunningAsync(closesAt: T0.AddSeconds(30));
        _clock.UtcNow = T0.AddSeconds(29);

        await using var db = NewDb();
        var store = new LiveSessionStore(db);
        Assert.Empty(await store.ListExpiredOpenSessionIdsAsync(_clock.UtcNow));
        Assert.False(await Handler(db).AutoCloseExpiredAsync(id, default));
        Assert.Equal(T0.AddSeconds(30), await store.GetNextPendingCloseAsync());
        Assert.Empty(_broadcasts);
    }

    [Fact]
    public async Task Concurrent_closers_produce_a_single_close()
    {
        var id = await SeedRunningAsync(closesAt: T0.AddSeconds(30));
        _clock.UtcNow = T0.AddSeconds(31);

        await using var db1 = NewDb();
        await using var db2 = NewDb();
        var h1 = Handler(db1);
        var h2 = Handler(db2);

        var results = await Task.WhenAll(
            h1.AutoCloseExpiredAsync(id, default),
            h2.AutoCloseExpiredAsync(id, default));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(1, _broadcasts.Count(b => b == nameof(ILiveSessionBroadcaster.QuestionClosedAsync)));
    }

    [Fact]
    public async Task Claim_is_atomic_and_does_not_close_a_reopened_or_future_question()
    {
        var id = await SeedRunningAsync(closesAt: T0.AddSeconds(30));
        await using var db = NewDb();
        var store = new LiveSessionStore(db);

        Assert.False(await store.TryClaimQuestionCloseAsync(id, 0, T0.AddSeconds(10), onlyIfExpired: true));
        Assert.False(await store.TryClaimQuestionCloseAsync(id, 1, T0.AddSeconds(31), onlyIfExpired: true));
        Assert.True(await store.TryClaimQuestionCloseAsync(id, 0, T0.AddSeconds(31), onlyIfExpired: true));
        Assert.False(await store.TryClaimQuestionCloseAsync(id, 0, T0.AddSeconds(32), onlyIfExpired: true));
        Assert.False(await store.TryClaimQuestionCloseAsync(id, 0, T0.AddSeconds(32), onlyIfExpired: false));
    }

    [Fact]
    public async Task Opening_the_next_question_makes_it_eligible_again()
    {
        var id = await SeedRunningAsync(closesAt: T0.AddSeconds(30));
        _clock.UtcNow = T0.AddSeconds(31);
        await using (var db = NewDb())
        {
            Assert.True(await Handler(db).AutoCloseExpiredAsync(id, default));
        }

        await using (var db = NewDb())
        {
            var session = await db.Set<LiveSession>().SingleAsync(s => s.Id == id);
            session.OpenQuestion(1, T0.AddSeconds(60), T0.AddSeconds(90));
            await db.SaveChangesAsync();
            Assert.Null(session.QuestionClosedAt);
        }

        await using (var db = NewDb())
        {
            var store = new LiveSessionStore(db);
            Assert.Empty(await store.ListExpiredOpenSessionIdsAsync(T0.AddSeconds(89)));
            Assert.Equal([id], await store.ListExpiredOpenSessionIdsAsync(T0.AddSeconds(90)));
        }
    }

    [Fact]
    public async Task Paused_and_ended_sessions_are_not_closed()
    {
        var paused = await SeedRunningAsync(closesAt: T0.AddSeconds(30), configure: s => s.Pause());
        var ended = await SeedRunningAsync(closesAt: T0.AddSeconds(30), configure: s => s.End(T0.AddSeconds(5)));
        _clock.UtcNow = T0.AddSeconds(60);

        await using var db = NewDb();
        var store = new LiveSessionStore(db);
        Assert.Empty(await store.ListExpiredOpenSessionIdsAsync(_clock.UtcNow));
        Assert.False(await Handler(db).AutoCloseExpiredAsync(paused, default));
        Assert.False(await Handler(db).AutoCloseExpiredAsync(ended, default));
    }

    [Fact]
    public void Timer_delay_follows_next_deadline_with_idle_cap_and_floor()
    {
        Assert.Equal(LiveQuestionTimerService.IdleDelay, LiveQuestionTimerService.NextDelay(null, T0));
        Assert.Equal(LiveQuestionTimerService.IdleDelay, LiveQuestionTimerService.NextDelay(T0.AddMinutes(5), T0));
        Assert.Equal(TimeSpan.FromSeconds(7), LiveQuestionTimerService.NextDelay(T0.AddSeconds(7), T0));
        Assert.Equal(LiveQuestionTimerService.MinDelay, LiveQuestionTimerService.NextDelay(T0.AddSeconds(-3), T0));
    }

    [Fact]
    public void Timer_error_backoff_grows_and_is_capped()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), LiveQuestionTimerService.ErrorDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(8), LiveQuestionTimerService.ErrorDelay(3));
        Assert.Equal(LiveQuestionTimerService.MaxErrorDelay, LiveQuestionTimerService.ErrorDelay(50));
    }

    [Fact]
    public async Task Signal_wakes_a_waiting_timer_and_coalesces()
    {
        var signal = new LiveQuestionTimerSignal();
        signal.Notify();
        signal.Notify();
        await signal.WaitAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signal.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task Schema_guard_adds_the_column_to_an_existing_table_idempotently()
    {
        await using var db = NewDb();
        await db.Database.ExecuteSqlRawAsync(
            """CREATE TABLE "LiveSessions" ("Id" INTEGER PRIMARY KEY, "QuestionClosesAt" TEXT NULL);""");

        await LiveSessionSchemaGuard.EnsureAsync(db);
        await LiveSessionSchemaGuard.EnsureAsync(db);

        var columns = await db.Database
            .SqlQueryRaw<string>("""SELECT name AS "Value" FROM pragma_table_info('LiveSessions')""")
            .ToListAsync();
        Assert.Contains("QuestionClosedAt", columns);
    }

    private async Task<int> SeedRunningAsync(DateTime closesAt, Action<LiveSession>? configure = null)
    {
        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();
        var session = LiveSession.Create(1, "Clase", Guid.NewGuid().ToString("N")[..8], LiveSessionModes.Competitive, 1, "{}", T0);
        session.SetQuestions(
        [
            LiveSessionQuestion.Create(0, 10, 0, Snapshot("Uno"), "Señales", null),
            LiveSessionQuestion.Create(0, 11, 1, Snapshot("Dos"), "Señales", null)
        ]);
        session.MarkRunning(T0);
        session.OpenQuestion(0, T0, closesAt);
        configure?.Invoke(session);
        db.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private static string Snapshot(string text) =>
        $$"""{"questionId":1,"text":"{{text}}","imageUrl":null,"topic":"Señales","explanation":null,"options":[{"id":1,"text":"A","imageUrl":null,"isCorrect":true},{"id":2,"text":"B","imageUrl":null,"isCorrect":false}]}""";

    private LiveSessionHandler Handler(CaleDbContext db) => new(
        new LiveSessionStore(db),
        Unused<ICatalogStore>(),
        Unused<ICatalogAccessGuard>(),
        RecordingProxy<ILiveSessionBroadcaster>.Create(_broadcasts),
        new LivePresentationSlideState(),
        _clock,
        new LiveQuestionTimerSignal());

    private CaleDbContext NewDb() => new(
        new DbContextOptionsBuilder<CaleDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=False;Default Timeout=30")
            .ReplaceService<IModelCacheKeyFactory, LiveOnlyModelKey>()
            .Options,
        new MappingAssemblies(typeof(LiveSessionConfiguration).Assembly));

    private static T Unused<T>() where T : class => RecordingProxy<T>.Create([]);

    private sealed class LiveOnlyModelKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => (context.GetType(), nameof(LiveOnlyModelKey), designTime);
    }

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; }
    }

    public class RecordingProxy<T> : DispatchProxy where T : class
    {
        private ConcurrentBag<string> _calls = [];

        public static T Create(ConcurrentBag<string> calls)
        {
            var proxy = Create<T, RecordingProxy<T>>();
            ((RecordingProxy<T>)(object)proxy)._calls = calls;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            _calls.Add(targetMethod!.Name);
            var type = targetMethod.ReturnType;
            if (type == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var inner = type.GetGenericArguments()[0];
                var value = inner.IsValueType ? Activator.CreateInstance(inner) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, [value]);
            }

            return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
        }
    }
}
