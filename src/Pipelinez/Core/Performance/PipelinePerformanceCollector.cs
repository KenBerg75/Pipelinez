using Ardalis.GuardClauses;

namespace Pipelinez.Core.Performance;

internal sealed class PipelinePerformanceCollector : IPipelinePerformanceCollector
{
    private sealed class ComponentAccumulator
    {
        public long ProcessedCount;
        public long FaultedCount;
        public long TotalDurationTicks;
    }

    private readonly object _syncLock = new();
    private readonly PipelineMetricsOptions _metricsOptions;
    private readonly Dictionary<string, ComponentAccumulator> _components = new(StringComparer.Ordinal);
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;

    private long _publishedCount;
    private long _completedCount;
    private long _faultedCount;
    private long _retryCount;
    private long _retryRecoveries;
    private long _retryExhaustions;
    private long _totalEndToEndLatencyTicks;

    public PipelinePerformanceCollector(PipelineMetricsOptions metricsOptions)
    {
        _metricsOptions = Guard.Against.Null(metricsOptions, nameof(metricsOptions));
    }

    public void RecordPublished(string componentName)
    {
        if (!_metricsOptions.EnableRuntimeMetrics)
        {
            return;
        }

        lock (_syncLock)
        {
            _publishedCount++;
            GetOrAddComponent(componentName).ProcessedCount++;
        }
    }

    public void RecordCompleted(DateTimeOffset createdAtUtc)
    {
        if (!_metricsOptions.EnableRuntimeMetrics)
        {
            return;
        }

        lock (_syncLock)
        {
            _completedCount++;
            _totalEndToEndLatencyTicks += (DateTimeOffset.UtcNow - createdAtUtc).Ticks;
        }
    }

    public void RecordFaulted(DateTimeOffset createdAtUtc)
    {
        if (!_metricsOptions.EnableRuntimeMetrics)
        {
            return;
        }

        lock (_syncLock)
        {
            _faultedCount++;
            _totalEndToEndLatencyTicks += (DateTimeOffset.UtcNow - createdAtUtc).Ticks;
        }
    }

    public void RecordComponentExecution(string componentName, TimeSpan duration, bool succeeded)
    {
        if (!_metricsOptions.EnableRuntimeMetrics)
        {
            return;
        }

        lock (_syncLock)
        {
            var component = GetOrAddComponent(componentName);
            component.ProcessedCount++;

            if (!succeeded)
            {
                component.FaultedCount++;
            }

            if (_metricsOptions.EnablePerComponentTiming)
            {
                component.TotalDurationTicks += duration.Ticks;
            }
        }
    }

    public void RecordRetryAttempt()
    {
        if (!_metricsOptions.EnableRuntimeMetrics)
        {
            return;
        }

        lock (_syncLock)
        {
            _retryCount++;
        }
    }

    public void RecordRetryRecovery()
    {
        if (!_metricsOptions.EnableRuntimeMetrics)
        {
            return;
        }

        lock (_syncLock)
        {
            _retryRecoveries++;
        }
    }

    public void RecordRetryExhausted()
    {
        if (!_metricsOptions.EnableRuntimeMetrics)
        {
            return;
        }

        lock (_syncLock)
        {
            _retryExhaustions++;
        }
    }

    public PipelinePerformanceSnapshot CreateSnapshot()
    {
        lock (_syncLock)
        {
            var elapsed = DateTimeOffset.UtcNow - _startedAtUtc;
            var elapsedSeconds = Math.Max(elapsed.TotalSeconds, 0.000001d);
            var totalFinished = _completedCount + _faultedCount;

            var componentSnapshots = _components
                .OrderBy(component => component.Key, StringComparer.Ordinal)
                .Select(component =>
                {
                    var accumulator = component.Value;
                    var averageDuration = accumulator.ProcessedCount == 0 || !_metricsOptions.EnablePerComponentTiming
                        ? TimeSpan.Zero
                        : TimeSpan.FromTicks(accumulator.TotalDurationTicks / accumulator.ProcessedCount);

                    return new PipelineComponentPerformanceSnapshot(
                        component.Key,
                        accumulator.ProcessedCount,
                        accumulator.FaultedCount,
                        accumulator.ProcessedCount / elapsedSeconds,
                        averageDuration);
                })
                .ToArray();

            var averageLatency = totalFinished == 0
                ? TimeSpan.Zero
                : TimeSpan.FromTicks(_totalEndToEndLatencyTicks / totalFinished);

            return new PipelinePerformanceSnapshot(
                _startedAtUtc,
                elapsed,
                _publishedCount,
                _completedCount,
                _faultedCount,
                _retryCount,
                _retryRecoveries,
                _retryExhaustions,
                totalFinished / elapsedSeconds,
                averageLatency,
                componentSnapshots);
        }
    }

    private ComponentAccumulator GetOrAddComponent(string componentName)
    {
        if (_components.TryGetValue(componentName, out var existing))
        {
            return existing;
        }

        var created = new ComponentAccumulator();
        _components[componentName] = created;
        return created;
    }
}
