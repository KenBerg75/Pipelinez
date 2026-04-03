using Ardalis.GuardClauses;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Eventing;


public delegate void PipelineContainerCompletedEventHandler<T>(object sender,
    PipelineContainerCompletedEventHandlerArgs<T> args);

/// <summary>
/// Contains data for the PipelineRecordCompleted event.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class PipelineContainerCompletedEventHandlerArgs<T>
{
    public T Container { get; }
    
    public PipelineContainerCompletedEventHandlerArgs(T container)
    {
        Container = container;
    }
}

public delegate void PipelineRecordCompletedEventHandler<T>(object sender, PipelineRecordCompletedEventHandlerArgs<T> args);

/// <summary>
/// Contains data for the PipelineRecordCompleted event.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class PipelineRecordCompletedEventHandlerArgs<T>
{
    public T Record { get; }

    public PipelineRecordDistributionContext? Distribution { get; }

    public PipelineRecordCompletedEventHandlerArgs(
        T record,
        PipelineRecordDistributionContext? distribution = null)
    {
        Record = record;
        Distribution = distribution;
    }
}

public delegate void PipelineRecordFaultedEventHandler<T>(
    object sender,
    PipelineRecordFaultedEventArgs<T> args) where T : PipelineRecord;

public delegate void PipelineRecordRetryingEventHandler<T>(
    object sender,
    PipelineRecordRetryingEventArgs<T> args) where T : PipelineRecord;

public delegate void PipelineSaturationChangedEventHandler(
    object sender,
    PipelineSaturationChangedEventArgs args);

public delegate void PipelinePublishRejectedEventHandler<T>(
    object sender,
    PipelinePublishRejectedEventArgs<T> args) where T : PipelineRecord;

public sealed class PipelineRecordFaultedEventArgs<T> where T : PipelineRecord
{
    public PipelineRecordFaultedEventArgs(
        T record,
        PipelineContainer<T> container,
        PipelineFaultState fault,
        PipelineRecordDistributionContext? distribution = null)
    {
        Record = Guard.Against.Null(record, nameof(record));
        Container = Guard.Against.Null(container, nameof(container));
        Fault = Guard.Against.Null(fault, nameof(fault));
        Distribution = distribution;
    }

    public T Record { get; }

    public PipelineContainer<T> Container { get; }

    public PipelineFaultState Fault { get; }

    public PipelineRecordDistributionContext? Distribution { get; }
}

public sealed class PipelineRecordRetryingEventArgs<T> where T : PipelineRecord
{
    public PipelineRecordRetryingEventArgs(
        T record,
        PipelineContainer<T> container,
        PipelineFaultState fault,
        int attemptNumber,
        int maxAttempts,
        TimeSpan delay,
        PipelineRecordDistributionContext? distribution = null)
    {
        Record = Guard.Against.Null(record, nameof(record));
        Container = Guard.Against.Null(container, nameof(container));
        Fault = Guard.Against.Null(fault, nameof(fault));
        AttemptNumber = Guard.Against.NegativeOrZero(attemptNumber, nameof(attemptNumber));
        MaxAttempts = Guard.Against.NegativeOrZero(maxAttempts, nameof(maxAttempts));
        Delay = delay;
        Distribution = distribution;
    }

    public T Record { get; }

    public PipelineContainer<T> Container { get; }

    public PipelineFaultState Fault { get; }

    public int AttemptNumber { get; }

    public int MaxAttempts { get; }

    public TimeSpan Delay { get; }

    public PipelineRecordDistributionContext? Distribution { get; }

    public string ComponentName => Fault.ComponentName;

    public PipelineComponentKind ComponentKind => Fault.ComponentKind;

    public Exception Exception => Fault.Exception;
}

public sealed class PipelineSaturationChangedEventArgs
{
    public PipelineSaturationChangedEventArgs(
        double saturationRatio,
        bool isSaturated,
        DateTimeOffset observedAtUtc)
    {
        SaturationRatio = saturationRatio;
        IsSaturated = isSaturated;
        ObservedAtUtc = observedAtUtc;
    }

    public double SaturationRatio { get; }

    public bool IsSaturated { get; }

    public DateTimeOffset ObservedAtUtc { get; }
}

public sealed class PipelinePublishRejectedEventArgs<T> where T : PipelineRecord
{
    public PipelinePublishRejectedEventArgs(
        T record,
        FlowControl.PipelinePublishResultReason reason,
        DateTimeOffset observedAtUtc)
    {
        Record = Guard.Against.Null(record, nameof(record));
        Reason = reason;
        ObservedAtUtc = observedAtUtc;
    }

    public T Record { get; }

    public FlowControl.PipelinePublishResultReason Reason { get; }

    public DateTimeOffset ObservedAtUtc { get; }
}

public delegate void PipelineFaultedEventHandler(object sender, PipelineFaultedEventArgs args);

public sealed class PipelineFaultedEventArgs
{
    public PipelineFaultedEventArgs(string pipelineName, PipelineFaultState fault)
    {
        PipelineName = Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));
        Fault = Guard.Against.Null(fault, nameof(fault));
    }

    public string PipelineName { get; }

    public PipelineFaultState Fault { get; }

    public Exception Exception => Fault.Exception;

    public string ComponentName => Fault.ComponentName;

    public PipelineComponentKind ComponentKind => Fault.ComponentKind;
}

public delegate void PipelineWorkerStartedEventHandler(object sender, PipelineWorkerStartedEventArgs args);

public sealed class PipelineWorkerStartedEventArgs
{
    public PipelineWorkerStartedEventArgs(PipelineRuntimeContext runtimeContext)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
    }

    public PipelineRuntimeContext RuntimeContext { get; }
}

public delegate void PipelineWorkerStoppingEventHandler(object sender, PipelineWorkerStoppingEventArgs args);

public sealed class PipelineWorkerStoppingEventArgs
{
    public PipelineWorkerStoppingEventArgs(PipelineRuntimeContext runtimeContext)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
    }

    public PipelineRuntimeContext RuntimeContext { get; }
}

public delegate void PipelinePartitionsAssignedEventHandler(object sender, PipelinePartitionsAssignedEventArgs args);

public sealed class PipelinePartitionsAssignedEventArgs
{
    public PipelinePartitionsAssignedEventArgs(
        PipelineRuntimeContext runtimeContext,
        IReadOnlyList<PipelinePartitionLease> partitions)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
        Partitions = Guard.Against.Null(partitions, nameof(partitions)).ToArray();
    }

    public PipelineRuntimeContext RuntimeContext { get; }

    public string WorkerId => RuntimeContext.WorkerId;

    public IReadOnlyList<PipelinePartitionLease> Partitions { get; }
}

public delegate void PipelinePartitionsRevokedEventHandler(object sender, PipelinePartitionsRevokedEventArgs args);

public sealed class PipelinePartitionsRevokedEventArgs
{
    public PipelinePartitionsRevokedEventArgs(
        PipelineRuntimeContext runtimeContext,
        IReadOnlyList<PipelinePartitionLease> partitions)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
        Partitions = Guard.Against.Null(partitions, nameof(partitions)).ToArray();
    }

    public PipelineRuntimeContext RuntimeContext { get; }

    public string WorkerId => RuntimeContext.WorkerId;

    public IReadOnlyList<PipelinePartitionLease> Partitions { get; }
}
