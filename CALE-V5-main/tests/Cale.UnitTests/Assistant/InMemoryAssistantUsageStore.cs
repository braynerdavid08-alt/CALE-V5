using Cale.Api.Services.Assistant;
using Cale.Modules.TheoreticalTraining.Application;

namespace Cale.UnitTests.Assistant;

/// <summary>Thread-safe in-memory usage store for unit tests (no EF).</summary>
public sealed class InMemoryAssistantUsageStore : IAssistantUsageStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(int UserId, DateOnly Day), (int Messages, int Requests, int? School)> _daily = new();

    private static DateOnly Today() => ColombiaTime.TodayInColombia();

    public Task<AssistantUsageSnapshot> GetSnapshotAsync(
        int userId,
        int? schoolUserId,
        int messagesLimit,
        CancellationToken ct)
    {
        lock (_gate)
        {
            _daily.TryGetValue((userId, Today()), out var row);
            var used = row.Messages;
            var remaining = Math.Max(0, messagesLimit - used);
            return Task.FromResult(new AssistantUsageSnapshot(
                userId,
                schoolUserId ?? row.School,
                Today(),
                used,
                messagesLimit,
                remaining,
                row.Requests,
                messagesLimit > 0 && used >= (int)Math.Ceiling(messagesLimit * 0.8),
                remaining <= 0));
        }
    }

    public Task<bool> TryReserveMessageAsync(
        int userId,
        int? schoolUserId,
        int messagesLimit,
        CancellationToken ct)
    {
        lock (_gate)
        {
            var key = (userId, Today());
            _daily.TryGetValue(key, out var row);
            if (row.Messages >= messagesLimit)
            {
                return Task.FromResult(false);
            }

            _daily[key] = (row.Messages + 1, row.Requests, schoolUserId ?? row.School);
            return Task.FromResult(true);
        }
    }

    public Task CommitMessageAsync(int userId, int? schoolUserId, CancellationToken ct) =>
        Task.CompletedTask;

    public Task ReleaseMessageAsync(int userId, int? schoolUserId, string reason, CancellationToken ct)
    {
        lock (_gate)
        {
            var key = (userId, Today());
            if (_daily.TryGetValue(key, out var row) && row.Messages > 0)
            {
                _daily[key] = (row.Messages - 1, row.Requests, schoolUserId ?? row.School);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryReserveProviderRequestAsync(
        int userId,
        int? schoolUserId,
        int userRequestLimit,
        int schoolRequestLimit,
        int globalRequestLimit,
        CancellationToken ct)
    {
        lock (_gate)
        {
            var day = Today();
            var key = (userId, day);
            _daily.TryGetValue(key, out var row);
            if (row.Requests >= userRequestLimit)
            {
                return Task.FromResult(false);
            }

            var schoolUsed = schoolUserId is int sid
                ? _daily.Where(kv => kv.Key.Day == day && kv.Value.School == sid).Sum(kv => kv.Value.Requests)
                : 0;
            if (schoolUsed >= schoolRequestLimit)
            {
                return Task.FromResult(false);
            }

            var globalUsed = _daily.Where(kv => kv.Key.Day == day).Sum(kv => kv.Value.Requests);
            if (globalUsed >= globalRequestLimit)
            {
                return Task.FromResult(false);
            }

            _daily[key] = (row.Messages, row.Requests + 1, schoolUserId ?? row.School);
            return Task.FromResult(true);
        }
    }

    public Task<AssistantSchoolUsageSummary> GetSchoolSummaryAsync(
        int schoolUserId,
        int messagesLimit,
        CancellationToken ct)
    {
        lock (_gate)
        {
            var day = Today();
            var rows = _daily
                .Where(kv => kv.Key.Day == day && kv.Value.School == schoolUserId)
                .Select(kv => new AssistantSchoolUsageRow(
                    kv.Key.UserId,
                    $"User {kv.Key.UserId}",
                    "Student",
                    kv.Value.Messages,
                    messagesLimit,
                    Math.Max(0, messagesLimit - kv.Value.Messages)))
                .ToList();
            return Task.FromResult(new AssistantSchoolUsageSummary(
                day,
                schoolUserId,
                rows.Sum(r => r.MessagesUsed),
                rows.Count,
                rows));
        }
    }

    public async Task<IReadOnlyList<AssistantSchoolUsageSummary>> ListSchoolsTodayAsync(
        int messagesLimit,
        CancellationToken ct)
    {
        List<int> ids;
        lock (_gate)
        {
            ids = _daily
                .Where(kv => kv.Key.Day == Today() && kv.Value.School is not null)
                .Select(kv => kv.Value.School!.Value)
                .Distinct()
                .ToList();
        }

        var list = new List<AssistantSchoolUsageSummary>();
        foreach (var id in ids)
        {
            list.Add(await GetSchoolSummaryAsync(id, messagesLimit, ct));
        }

        return list;
    }
}
