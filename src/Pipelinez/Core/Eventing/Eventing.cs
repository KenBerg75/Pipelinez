using Ardalis.GuardClauses;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Operational;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Eventing;


/// <summary>
/// Represents a handler for internal pipeline-container completion notifications.
/// </summary>
/// <typeparam name="T">The completed container type.</typeparam>
public delegate void PipelineContainerCompletedEventHandler<T>(object sender,
    PipelineContainerCompletedEventHandlerArgs<T> args);

internal delegate void PipelineContainerFaultHandledEventHandler<T>(object sender,
    PipelineContainerFaultHandledEventHandlerArgs<T> args);

/// <summary>
/// Contains data for an internal pipeline-container completion notification.
/// </summary>
/// <typeparam name="T">The completed container type.</typeparam>
public sealed class PipelineContainerCompletedEventHandlerArgs<T>
{
    /// <summary>
    /// Gets the completed container.
    /// </summary>
    public T Container { get; }

    /// <summary>
    /// Initializes a new container completion event payload.
    /// </summary>
    /// <param name="container">The completed container.</param>
    public PipelineContainerCompletedEventHandlerArgs(T container)
    {
        Container = container;
    }
}

internal sealed class PipelineContainerFaultHandledEventHandlerArgs<T>
{
    public PipelineContainerFaultHandledEventHandlerArgs(T container, ErrorHandling.PipelineErrorAction action)
    {
        Container = container;
        Action = action;
    }

    public T Container { get; }

    public ErrorHandling.PipelineErrorAction Action { get; }
}

/// <summary>
/// Represents a handler for record-completed notifications.
/// </summary>
/// <typeparam name="T">The completed record type.</typeparam>
public delegate void PipelineRecordCompletedEventHandler<T>(object sender, PipelineRecordCompletedEventHandlerArgs<T> args);

/// <summary>
/// Contains data for the record-completed event.
/// </summary>
/// <typeparam name="T">The completed record type.</typeparam>
public sealed class PipelineRecordCompletedEventHandlerArgs<T>
{
    /// <summary>
    /// Gets the completed record.
    /// </summary>
    public T Record { get; }

    /// <summary>
    /// Gets the distributed execution context for the record, if one exists.
    /// </summary>
    public PipelineRecordDistributionContext? Distribution { get; }

    /// <summary>
    /// Gets the diagnostic context for the record, if one exists.
    /// </summary>
    public PipelineRecordDiagnosticContext? Diagnostic { get; }

    /// <summary>
    /// Initializes a new record-completed event payload.
    /// </summary>
    /// <param name="record">The completed record.</param>
    /// <param name="distribution">The distributed execution context, if one exists.</param>
    /// <param name="diagnostic">The diagnostic context, if one exists.</param>
    public PipelineRecordCompletedEventHandlerArgs(
        T record,
        PipelineRecordDistributionContext? distribution = null,
        PipelineRecordDiagnosticContext? diagnostic = null)
    {
        Record = record;
        Distribution = distribution;
        Diagnostic = diagnostic;
    }
}

/// <summary>
/// Represents a handler for record-faulted notifications.
/// </summary>
/// <typeparam name="T">The faulted record type.</typeparam>
public delegate void PipelineRecordFaultedEventHandler<T>(
    object sender,
    PipelineRecordFaultedEventArgs<T> args) where T : PipelineRecord;

/// <summary>
/// Represents a handler for record-retrying notifications.
/// </summary>
/// <typeparam name="T">The retried record type.</typeparam>
public delegate void PipelineRecordRetryingEventHandler<T>(
    object sender,
    PipelineRecordRetryingEventArgs<T> args) where T : PipelineRecord;

/// <summary>
/// Represents a handler for saturation-state change notifications.
/// </summary>
public delegate void PipelineSaturationChangedEventHandler(
    object sender,
    PipelineSaturationChangedEventArgs args);

/// <summary>
/// Represents a handler for publish-rejected notifications.
/// </summary>
/// <typeparam name="T">The rejected record type.</typeparam>
public delegate void PipelinePublishRejectedEventHandler<T>(
    object sender,
    PipelinePublishRejectedEventArgs<T> args) where T : PipelineRecord;

/// <summary>
/// Represents a handler for record dead-letter notifications.
/// </summary>
/// <typeparam name="T">The dead-lettered record type.</typeparam>
public delegate void PipelineRecordDeadLetteredEventHandler<T>(
    object sender,
    PipelineRecordDeadLetteredEventArgs<T> args) where T : PipelineRecord;

/// <summary>
/// Represents a handler for dead-letter-write-failed notifications.
/// </summary>
/// <typeparam name="T">The record type associated with the failed dead-letter write.</typeparam>
public delegate void PipelineDeadLetterWriteFailedEventHandler<T>(
    object sender,
    PipelineDeadLetterWriteFailedEventArgs<T> args) where T : PipelineRecord;

/// <summary>
/// Contains data for the record-faulted event.
/// </summary>
/// <typeparam name="T">The faulted record type.</typeparam>
public sealed class PipelineRecordFaultedEventArgs<T> where T : PipelineRecord
{
    /// <summary>
    /// Initializes a new record-faulted event payload.
    /// </summary>
    public PipelineRecordFaultedEventArgs(
        T record,
        PipelineContainer<T> container,
        PipelineFaultState fault,
        PipelineRecordDistributionContext? distribution = null,
        PipelineRecordDiagnosticContext? diagnostic = null)
    {
        Record = Guard.Against.Null(record, nameof(record));
        Container = Guard.Against.Null(container, nameof(container));
        Fault = Guard.Against.Null(fault, nameof(fault));
        Distribution = distribution;
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the faulted record.
    /// </summary>
    public T Record { get; }

    /// <summary>
    /// Gets the faulted container.
    /// </summary>
    public PipelineContainer<T> Container { get; }

    /// <summary>
    /// Gets the recorded fault state.
    /// </summary>
    public PipelineFaultState Fault { get; }

    /// <summary>
    /// Gets the distributed execution context for the record, if one exists.
    /// </summary>
    public PipelineRecordDistributionContext? Distribution { get; }

    /// <summary>
    /// Gets the diagnostic context for the record, if one exists.
    /// </summary>
    public PipelineRecordDiagnosticContext? Diagnostic { get; }
}

/// <summary>
/// Contains data for the record-retrying event.
/// </summary>
/// <typeparam name="T">The retried record type.</typeparam>
public sealed class PipelineRecordRetryingEventArgs<T> where T : PipelineRecord
{
    /// <summary>
    /// Initializes a new record-retrying event payload.
    /// </summary>
    public PipelineRecordRetryingEventArgs(
        T record,
        PipelineContainer<T> container,
        PipelineFaultState fault,
        int attemptNumber,
        int maxAttempts,
        TimeSpan delay,
        PipelineRecordDistributionContext? distribution = null,
        PipelineRecordDiagnosticContext? diagnostic = null)
    {
        Record = Guard.Against.Null(record, nameof(record));
        Container = Guard.Against.Null(container, nameof(container));
        Fault = Guard.Against.Null(fault, nameof(fault));
        AttemptNumber = Guard.Against.NegativeOrZero(attemptNumber, nameof(attemptNumber));
        MaxAttempts = Guard.Against.NegativeOrZero(maxAttempts, nameof(maxAttempts));
        Delay = delay;
        Distribution = distribution;
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the record being retried.
    /// </summary>
    public T Record { get; }

    /// <summary>
    /// Gets the container associated with the retry.
    /// </summary>
    public PipelineContainer<T> Container { get; }

    /// <summary>
    /// Gets the recorded fault state that triggered the retry.
    /// </summary>
    public PipelineFaultState Fault { get; }

    /// <summary>
    /// Gets the current retry attempt number.
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets the maximum number of attempts allowed.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the delay scheduled before the next attempt.
    /// </summary>
    public TimeSpan Delay { get; }

    /// <summary>
    /// Gets the distributed execution context for the record, if one exists.
    /// </summary>
    public PipelineRecordDistributionContext? Distribution { get; }

    /// <summary>
    /// Gets the diagnostic context for the record, if one exists.
    /// </summary>
    public PipelineRecordDiagnosticContext? Diagnostic { get; }

    /// <summary>
    /// Gets the component name associated with the retry.
    /// </summary>
    public string ComponentName => Fault.ComponentName;

    /// <summary>
    /// Gets the component kind associated with the retry.
    /// </summary>
    public PipelineComponentKind ComponentKind => Fault.ComponentKind;

    /// <summary>
    /// Gets the exception that triggered the retry.
    /// </summary>
    public Exception Exception => Fault.Exception;
}

/// <summary>
/// Contains data for saturation-state change notifications.
/// </summary>
public sealed class PipelineSaturationChangedEventArgs
{
    /// <summary>
    /// Initializes a new saturation-change payload.
    /// </summary>
    public PipelineSaturationChangedEventArgs(
        double saturationRatio,
        bool isSaturated,
        DateTimeOffset observedAtUtc)
    {
        SaturationRatio = saturationRatio;
        IsSaturated = isSaturated;
        ObservedAtUtc = observedAtUtc;
    }

    /// <summary>
    /// Gets the observed saturation ratio.
    /// </summary>
    public double SaturationRatio { get; }

    /// <summary>
    /// Gets a value indicating whether the pipeline was saturated.
    /// </summary>
    public bool IsSaturated { get; }

    /// <summary>
    /// Gets the time the saturation state was observed.
    /// </summary>
    public DateTimeOffset ObservedAtUtc { get; }
}

/// <summary>
/// Contains data for publish-rejected notifications.
/// </summary>
/// <typeparam name="T">The rejected record type.</typeparam>
public sealed class PipelinePublishRejectedEventArgs<T> where T : PipelineRecord
{
    /// <summary>
    /// Initializes a new publish-rejected payload.
    /// </summary>
    public PipelinePublishRejectedEventArgs(
        T record,
        FlowControl.PipelinePublishResultReason reason,
        DateTimeOffset observedAtUtc,
        PipelineRecordDiagnosticContext? diagnostic = null)
    {
        Record = Guard.Against.Null(record, nameof(record));
        Reason = reason;
        ObservedAtUtc = observedAtUtc;
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the rejected record.
    /// </summary>
    public T Record { get; }

    /// <summary>
    /// Gets the rejection reason.
    /// </summary>
    public FlowControl.PipelinePublishResultReason Reason { get; }

    /// <summary>
    /// Gets the time the rejection was observed.
    /// </summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary>
    /// Gets the diagnostic context for the record, if one exists.
    /// </summary>
    public PipelineRecordDiagnosticContext? Diagnostic { get; }
}

/// <summary>
/// Contains data for record-dead-lettered notifications.
/// </summary>
/// <typeparam name="T">The dead-lettered record type.</typeparam>
public sealed class PipelineRecordDeadLetteredEventArgs<T> where T : PipelineRecord
{
    /// <summary>
    /// Initializes a new record-dead-lettered payload.
    /// </summary>
    public PipelineRecordDeadLetteredEventArgs(
        T record,
        PipelineDeadLetterRecord<T> deadLetterRecord,
        PipelineRecordDiagnosticContext? diagnostic = null)
    {
        Record = Guard.Against.Null(record, nameof(record));
        DeadLetterRecord = Guard.Against.Null(deadLetterRecord, nameof(deadLetterRecord));
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the dead-lettered record.
    /// </summary>
    public T Record { get; }

    /// <summary>
    /// Gets the dead-letter envelope written for the record.
    /// </summary>
    public PipelineDeadLetterRecord<T> DeadLetterRecord { get; }

    /// <summary>
    /// Gets the diagnostic context for the record, if one exists.
    /// </summary>
    public PipelineRecordDiagnosticContext? Diagnostic { get; }
}

/// <summary>
/// Contains data for dead-letter-write-failed notifications.
/// </summary>
/// <typeparam name="T">The record type associated with the failed dead-letter write.</typeparam>
public sealed class PipelineDeadLetterWriteFailedEventArgs<T> where T : PipelineRecord
{
    /// <summary>
    /// Initializes a new dead-letter-write-failed payload.
    /// </summary>
    public PipelineDeadLetterWriteFailedEventArgs(
        T record,
        PipelineDeadLetterRecord<T> deadLetterRecord,
        Exception exception,
        PipelineRecordDiagnosticContext? diagnostic = null)
    {
        Record = Guard.Against.Null(record, nameof(record));
        DeadLetterRecord = Guard.Against.Null(deadLetterRecord, nameof(deadLetterRecord));
        Exception = Guard.Against.Null(exception, nameof(exception));
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the record associated with the failed dead-letter write.
    /// </summary>
    public T Record { get; }

    /// <summary>
    /// Gets the dead-letter envelope that failed to write.
    /// </summary>
    public PipelineDeadLetterRecord<T> DeadLetterRecord { get; }

    /// <summary>
    /// Gets the exception raised by the dead-letter destination.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the diagnostic context for the record, if one exists.
    /// </summary>
    public PipelineRecordDiagnosticContext? Diagnostic { get; }
}

/// <summary>
/// Represents a handler for pipeline-faulted notifications.
/// </summary>
public delegate void PipelineFaultedEventHandler(object sender, PipelineFaultedEventArgs args);

/// <summary>
/// Contains data for pipeline-faulted notifications.
/// </summary>
public sealed class PipelineFaultedEventArgs
{
    /// <summary>
    /// Initializes a new pipeline-faulted payload.
    /// </summary>
    public PipelineFaultedEventArgs(string pipelineName, PipelineFaultState fault)
    {
        PipelineName = Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));
        Fault = Guard.Against.Null(fault, nameof(fault));
    }

    /// <summary>
    /// Gets the logical pipeline name.
    /// </summary>
    public string PipelineName { get; }

    /// <summary>
    /// Gets the recorded pipeline fault.
    /// </summary>
    public PipelineFaultState Fault { get; }

    /// <summary>
    /// Gets the underlying exception.
    /// </summary>
    public Exception Exception => Fault.Exception;

    /// <summary>
    /// Gets the faulting component name.
    /// </summary>
    public string ComponentName => Fault.ComponentName;

    /// <summary>
    /// Gets the kind of component that faulted.
    /// </summary>
    public PipelineComponentKind ComponentKind => Fault.ComponentKind;
}

/// <summary>
/// Represents a handler for worker-started notifications.
/// </summary>
public delegate void PipelineWorkerStartedEventHandler(object sender, PipelineWorkerStartedEventArgs args);

/// <summary>
/// Contains data for worker-started notifications.
/// </summary>
public sealed class PipelineWorkerStartedEventArgs
{
    /// <summary>
    /// Initializes a new worker-started payload.
    /// </summary>
    public PipelineWorkerStartedEventArgs(PipelineRuntimeContext runtimeContext)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
    }

    /// <summary>
    /// Gets the current runtime context.
    /// </summary>
    public PipelineRuntimeContext RuntimeContext { get; }
}

/// <summary>
/// Represents a handler for worker-stopping notifications.
/// </summary>
public delegate void PipelineWorkerStoppingEventHandler(object sender, PipelineWorkerStoppingEventArgs args);

/// <summary>
/// Contains data for worker-stopping notifications.
/// </summary>
public sealed class PipelineWorkerStoppingEventArgs
{
    /// <summary>
    /// Initializes a new worker-stopping payload.
    /// </summary>
    public PipelineWorkerStoppingEventArgs(PipelineRuntimeContext runtimeContext)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
    }

    /// <summary>
    /// Gets the current runtime context.
    /// </summary>
    public PipelineRuntimeContext RuntimeContext { get; }
}

/// <summary>
/// Represents a handler for partition-assigned notifications.
/// </summary>
public delegate void PipelinePartitionsAssignedEventHandler(object sender, PipelinePartitionsAssignedEventArgs args);

/// <summary>
/// Contains data for partition-assigned notifications.
/// </summary>
public sealed class PipelinePartitionsAssignedEventArgs
{
    /// <summary>
    /// Initializes a new partition-assigned payload.
    /// </summary>
    public PipelinePartitionsAssignedEventArgs(
        PipelineRuntimeContext runtimeContext,
        IReadOnlyList<PipelinePartitionLease> partitions)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
        Partitions = Guard.Against.Null(partitions, nameof(partitions)).ToArray();
    }

    /// <summary>
    /// Gets the current runtime context.
    /// </summary>
    public PipelineRuntimeContext RuntimeContext { get; }

    /// <summary>
    /// Gets the current worker identifier.
    /// </summary>
    public string WorkerId => RuntimeContext.WorkerId;

    /// <summary>
    /// Gets the partitions assigned to the worker.
    /// </summary>
    public IReadOnlyList<PipelinePartitionLease> Partitions { get; }
}

/// <summary>
/// Represents a handler for partition-revoked notifications.
/// </summary>
public delegate void PipelinePartitionsRevokedEventHandler(object sender, PipelinePartitionsRevokedEventArgs args);
/// <summary>
/// Represents a handler for partition-draining notifications.
/// </summary>
public delegate void PipelinePartitionDrainingEventHandler(object sender, PipelinePartitionDrainingEventArgs args);
/// <summary>
/// Represents a handler for partition-drained notifications.
/// </summary>
public delegate void PipelinePartitionDrainedEventHandler(object sender, PipelinePartitionDrainedEventArgs args);
/// <summary>
/// Represents a handler for partition-execution-state-changed notifications.
/// </summary>
public delegate void PipelinePartitionExecutionStateChangedEventHandler(object sender, PipelinePartitionExecutionStateChangedEventArgs args);

/// <summary>
/// Contains data for partition-revoked notifications.
/// </summary>
public sealed class PipelinePartitionsRevokedEventArgs
{
    /// <summary>
    /// Initializes a new partition-revoked payload.
    /// </summary>
    public PipelinePartitionsRevokedEventArgs(
        PipelineRuntimeContext runtimeContext,
        IReadOnlyList<PipelinePartitionLease> partitions)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
        Partitions = Guard.Against.Null(partitions, nameof(partitions)).ToArray();
    }

    /// <summary>
    /// Gets the current runtime context.
    /// </summary>
    public PipelineRuntimeContext RuntimeContext { get; }

    /// <summary>
    /// Gets the current worker identifier.
    /// </summary>
    public string WorkerId => RuntimeContext.WorkerId;

    /// <summary>
    /// Gets the partitions revoked from the worker.
    /// </summary>
    public IReadOnlyList<PipelinePartitionLease> Partitions { get; }
}

/// <summary>
/// Contains data for partition-draining notifications.
/// </summary>
public sealed class PipelinePartitionDrainingEventArgs
{
    /// <summary>
    /// Initializes a new partition-draining payload.
    /// </summary>
    public PipelinePartitionDrainingEventArgs(
        PipelineRuntimeContext runtimeContext,
        PipelinePartitionLease partition)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
        Partition = Guard.Against.Null(partition, nameof(partition));
    }

    /// <summary>
    /// Gets the current runtime context.
    /// </summary>
    public PipelineRuntimeContext RuntimeContext { get; }

    /// <summary>
    /// Gets the partition that is draining.
    /// </summary>
    public PipelinePartitionLease Partition { get; }
}

/// <summary>
/// Contains data for partition-drained notifications.
/// </summary>
public sealed class PipelinePartitionDrainedEventArgs
{
    /// <summary>
    /// Initializes a new partition-drained payload.
    /// </summary>
    public PipelinePartitionDrainedEventArgs(
        PipelineRuntimeContext runtimeContext,
        PipelinePartitionLease partition)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
        Partition = Guard.Against.Null(partition, nameof(partition));
    }

    /// <summary>
    /// Gets the current runtime context.
    /// </summary>
    public PipelineRuntimeContext RuntimeContext { get; }

    /// <summary>
    /// Gets the partition that finished draining.
    /// </summary>
    public PipelinePartitionLease Partition { get; }
}

/// <summary>
/// Contains data for partition-execution-state-changed notifications.
/// </summary>
public sealed class PipelinePartitionExecutionStateChangedEventArgs
{
    /// <summary>
    /// Initializes a new partition-execution-state-changed payload.
    /// </summary>
    public PipelinePartitionExecutionStateChangedEventArgs(
        PipelineRuntimeContext runtimeContext,
        Distributed.PipelinePartitionExecutionState state)
    {
        RuntimeContext = Guard.Against.Null(runtimeContext, nameof(runtimeContext));
        State = Guard.Against.Null(state, nameof(state));
    }

    /// <summary>
    /// Gets the current runtime context.
    /// </summary>
    public PipelineRuntimeContext RuntimeContext { get; }

    /// <summary>
    /// Gets the updated partition execution state.
    /// </summary>
    public Distributed.PipelinePartitionExecutionState State { get; }
}
