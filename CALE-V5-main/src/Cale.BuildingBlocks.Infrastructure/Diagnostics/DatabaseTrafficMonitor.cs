using System.Diagnostics.Metrics;

namespace Cale.BuildingBlocks.Infrastructure.Diagnostics;

/// <summary>
/// Aggregates the metrics the database driver publishes (Npgsql 8: bytes read/written, command
/// durations, connection creation…) into per-window totals. Instruments are handled by kind, not
/// by name, so driver renames do not break it. Tags are only used as keys and never exposed.
/// </summary>
public sealed class DatabaseTrafficMonitor : IDisposable
{
    public const string NpgsqlMeterName = "Npgsql";

    private readonly object _gate = new();
    private readonly MeterListener _listener;
    private readonly Dictionary<string, InstrumentState> _instruments = new(StringComparer.Ordinal);

    public DatabaseTrafficMonitor(string meterName = NpgsqlMeterName)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        _listener.SetMeasurementEventCallback<long>((i, v, tags, _) => OnMeasurement(i, v, tags));
        _listener.SetMeasurementEventCallback<int>((i, v, tags, _) => OnMeasurement(i, v, tags));
        _listener.SetMeasurementEventCallback<double>((i, v, tags, _) => OnMeasurement(i, v, tags));
        _listener.Start();
    }

    /// <summary>Values for the window since the previous snapshot, keyed by instrument name.</summary>
    public DatabaseTrafficSnapshot TakeSnapshot()
    {
        _listener.RecordObservableInstruments();
        lock (_gate)
        {
            var values = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var (name, state) in _instruments)
            {
                values[name] = state.TakeWindowValue();
            }

            return new DatabaseTrafficSnapshot(values);
        }
    }

    public void Dispose() => _listener.Dispose();

    private void OnMeasurement(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tagKey = TagKey(tags);
        lock (_gate)
        {
            if (!_instruments.TryGetValue(instrument.Name, out var state))
            {
                state = new InstrumentState(KindOf(instrument));
                _instruments[instrument.Name] = state;
            }

            state.Add(tagKey, value);
        }
    }

    private static InstrumentKind KindOf(Instrument instrument)
    {
        var type = instrument.GetType();
        var name = type.IsGenericType ? type.GetGenericTypeDefinition().Name : type.Name;
        return name switch
        {
            _ when name.StartsWith("ObservableCounter", StringComparison.Ordinal) => InstrumentKind.CumulativeTotal,
            _ when name.StartsWith("ObservableUpDownCounter", StringComparison.Ordinal) => InstrumentKind.CurrentValue,
            _ when name.StartsWith("ObservableGauge", StringComparison.Ordinal) => InstrumentKind.CurrentValue,
            _ when name.StartsWith("Histogram", StringComparison.Ordinal) => InstrumentKind.EventCount,
            _ when name.StartsWith("UpDownCounter", StringComparison.Ordinal) => InstrumentKind.RunningTotal,
            _ => InstrumentKind.Increment
        };
    }

    private static string TagKey(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0)
        {
            return string.Empty;
        }

        var parts = new string[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            parts[i] = tags[i].Key + "=" + tags[i].Value;
        }

        Array.Sort(parts, StringComparer.Ordinal);
        return string.Join('|', parts);
    }

    private enum InstrumentKind
    {
        /// <summary>Counter: each measurement is an increment; reset every window.</summary>
        Increment,
        /// <summary>UpDownCounter: increments that describe a level (e.g. commands executing).</summary>
        RunningTotal,
        /// <summary>Histogram: number of recorded events in the window (e.g. commands run).</summary>
        EventCount,
        /// <summary>ObservableCounter: monotonic total per tag set; the window value is the delta.</summary>
        CumulativeTotal,
        /// <summary>ObservableUpDownCounter/Gauge: latest value per tag set, summed.</summary>
        CurrentValue
    }

    private sealed class InstrumentState(InstrumentKind kind)
    {
        private readonly Dictionary<string, double> _latest = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _previous = new(StringComparer.Ordinal);
        private double _window;
        private double _running;

        public void Add(string tagKey, double value)
        {
            switch (kind)
            {
                case InstrumentKind.Increment:
                    _window += value;
                    break;
                case InstrumentKind.EventCount:
                    _window += 1;
                    break;
                case InstrumentKind.RunningTotal:
                    _running += value;
                    break;
                default:
                    _latest[tagKey] = value;
                    break;
            }
        }

        public double TakeWindowValue()
        {
            switch (kind)
            {
                case InstrumentKind.RunningTotal:
                    return _running;
                case InstrumentKind.CurrentValue:
                    return _latest.Values.Sum();
                case InstrumentKind.CumulativeTotal:
                {
                    double delta = 0;
                    foreach (var (key, total) in _latest)
                    {
                        var before = _previous.GetValueOrDefault(key);
                        delta += total >= before ? total - before : total;
                        _previous[key] = total;
                    }

                    return delta;
                }
                default:
                {
                    var value = _window;
                    _window = 0;
                    return value;
                }
            }
        }
    }
}

public sealed record DatabaseTrafficSnapshot(IReadOnlyDictionary<string, double> Values)
{
    public long BytesRead => (long)Sum("bytes_read");
    public long BytesWritten => (long)Sum("bytes_written");

    private double Sum(string suffix) =>
        Values.Where(kv => kv.Key.EndsWith(suffix, StringComparison.Ordinal)).Sum(kv => kv.Value);
}
