using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Pipelinez.Core.Distributed;

namespace Pipelinez.Core.Operational;

internal sealed class PipelineMetricsEmitter : IPipelineMetricsEmitter, IDisposable
{
    internal const string MeterName = "Pipelinez.Runtime";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RecordsPublishedCounter = Meter.CreateCounter<long>("pipelinez.records.published");
    private static readonly Counter<long> RecordsCompletedCounter = Meter.CreateCounter<long>("pipelinez.records.completed");
    private static readonly Counter<long> RecordsFaultedCounter = Meter.CreateCounter<long>("pipelinez.records.faulted");
    private static readonly Counter<long> RetryAttemptsCounter = Meter.CreateCounter<long>("pipelinez.retry.attempts");
    private static readonly Counter<long> RetryRecoveriesCounter = Meter.CreateCounter<long>("pipelinez.retry.recoveries");
    private static readonly Counter<long> RetryExhaustionsCounter = Meter.CreateCounter<long>("pipelinez.retry.exhaustions");
    private static readonly Counter<long> DeadLetteredCounter = Meter.CreateCounter<long>("pipelinez.deadletter.count");
    private static readonly Counter<long> DeadLetterFailuresCounter = Meter.CreateCounter<long>("pipelinez.deadletter.failures");
    private static readonly Counter<long> PublishRejectedCounter = Meter.CreateCounter<long>("pipelinez.publish.rejected");
    private static readonly ConcurrentDictionary<Guid, PipelineMetricsEmitter> Emitters = new();
    private static readonly ObservableGauge<int> BufferedRecordsGauge = Meter.CreateObservableGauge<int>(
        "pipelinez.buffered_records",
        ObserveBufferedRecords);
    private static readonly ObservableGauge<int> OwnedPartitionsGauge = Meter.CreateObservableGauge<int>(
        "pipelinez.owned_partitions",
        ObserveOwnedPartitions);
    private static readonly ObservableGauge<double> RecordsPerSecondGauge = Meter.CreateObservableGauge<double>(
        "pipelinez.records_per_second",
        ObserveRecordsPerSecond);
    private static readonly ObservableGauge<int> HealthStateGauge = Meter.CreateObservableGauge<int>(
        "pipelinez.health_state",
        ObserveHealthState);

    private readonly Guid _registrationId = Guid.NewGuid();
    private readonly string _pipelineName;
    private readonly string _workerId;
    private readonly PipelineExecutionMode _executionMode;
    private readonly Func<double> _recordsPerSecondProvider;
    private readonly Func<PipelineHealthState> _healthStateProvider;
    private int _bufferedCount;
    private int _ownedPartitionCount;

    public PipelineMetricsEmitter(
        string pipelineName,
        string workerId,
        PipelineExecutionMode executionMode,
        Func<double> recordsPerSecondProvider,
        Func<PipelineHealthState> healthStateProvider)
    {
        _pipelineName = pipelineName;
        _workerId = workerId;
        _executionMode = executionMode;
        _recordsPerSecondProvider = recordsPerSecondProvider;
        _healthStateProvider = healthStateProvider;
        Emitters[_registrationId] = this;
    }

    public void Dispose()
    {
        Emitters.TryRemove(_registrationId, out _);
    }

    public void RecordPublished()
    {
        RecordsPublishedCounter.Add(1, BuildTags());
    }

    public void RecordCompleted()
    {
        RecordsCompletedCounter.Add(1, BuildTags());
    }

    public void RecordFaulted()
    {
        RecordsFaultedCounter.Add(1, BuildTags());
    }

    public void RecordRetryAttempt()
    {
        RetryAttemptsCounter.Add(1, BuildTags());
    }

    public void RecordRetryRecovery()
    {
        RetryRecoveriesCounter.Add(1, BuildTags());
    }

    public void RecordRetryExhausted()
    {
        RetryExhaustionsCounter.Add(1, BuildTags());
    }

    public void RecordDeadLettered()
    {
        DeadLetteredCounter.Add(1, BuildTags());
    }

    public void RecordDeadLetterFailure()
    {
        DeadLetterFailuresCounter.Add(1, BuildTags());
    }

    public void RecordPublishRejected()
    {
        PublishRejectedCounter.Add(1, BuildTags());
    }

    public void ObserveBufferedCount(int totalBufferedCount)
    {
        _bufferedCount = totalBufferedCount;
    }

    public void ObserveOwnedPartitionCount(int ownedPartitionCount)
    {
        _ownedPartitionCount = ownedPartitionCount;
    }

    private TagList BuildTags()
    {
        return new TagList
        {
            { "pipeline.name", _pipelineName },
            { "pipeline.mode", _executionMode.ToString() },
            { "worker.id", _workerId }
        };
    }

    private Measurement<int> GetBufferedMeasurement() => new(_bufferedCount, BuildTags());

    private Measurement<int> GetOwnedPartitionsMeasurement() => new(_ownedPartitionCount, BuildTags());

    private Measurement<double> GetRecordsPerSecondMeasurement() => new(_recordsPerSecondProvider(), BuildTags());

    private Measurement<int> GetHealthStateMeasurement() => new((int)_healthStateProvider(), BuildTags());

    private static IEnumerable<Measurement<int>> ObserveBufferedRecords()
    {
        return Emitters.Values.Select(emitter => emitter.GetBufferedMeasurement()).ToArray();
    }

    private static IEnumerable<Measurement<int>> ObserveOwnedPartitions()
    {
        return Emitters.Values.Select(emitter => emitter.GetOwnedPartitionsMeasurement()).ToArray();
    }

    private static IEnumerable<Measurement<double>> ObserveRecordsPerSecond()
    {
        return Emitters.Values.Select(emitter => emitter.GetRecordsPerSecondMeasurement()).ToArray();
    }

    private static IEnumerable<Measurement<int>> ObserveHealthState()
    {
        return Emitters.Values.Select(emitter => emitter.GetHealthStateMeasurement()).ToArray();
    }
}
