using Pipelinez.Core.Distributed;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Operational;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;
using Pipelinez.Core.Status;

namespace Pipelinez.Core;

/// <summary>
/// Represents a running pipeline that can accept, process, and complete records.
/// </summary>
/// <typeparam name="T">The pipeline record type processed by the pipeline.</typeparam>
public interface IPipeline<T> where T : PipelineRecord
{
    /// <summary>
    /// Starts the pipeline runtime.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel startup or runtime execution.</param>
    /// <returns>A task that completes once startup has finished.</returns>
    Task StartPipelineAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Publishes a record into the pipeline using the default flow-control behavior.
    /// </summary>
    /// <param name="record">The record to publish.</param>
    Task PublishAsync(T record);

    /// <summary>
    /// Publishes a record into the pipeline with explicit flow-control behavior.
    /// </summary>
    /// <param name="record">The record to publish.</param>
    /// <param name="options">The publish options to apply.</param>
    /// <returns>The publish result.</returns>
    Task<PipelinePublishResult> PublishAsync(T record, PipelinePublishOptions options);

    /// <summary>
    /// Completes and shuts down the pipeline.
    /// </summary>
    /// <returns>A task that completes once the pipeline has fully shut down.</returns>
    Task CompleteAsync();

    /// <summary>Gets a <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation and completion of the pipeline.</summary>
    /// <returns>The task.</returns>
    Task Completion { get; }

    /// <summary>
    /// Gets the current runtime status snapshot for the pipeline.
    /// </summary>
    /// <returns>The current pipeline status.</returns>
    PipelineStatus GetStatus();

    /// <summary>
    /// Gets the current distributed runtime context for the pipeline.
    /// </summary>
    /// <returns>The current runtime context.</returns>
    PipelineRuntimeContext GetRuntimeContext();

    /// <summary>
    /// Gets the current performance snapshot for the pipeline.
    /// </summary>
    /// <returns>The current performance snapshot.</returns>
    PipelinePerformanceSnapshot GetPerformanceSnapshot();

    /// <summary>
    /// Gets the current health snapshot for the pipeline.
    /// </summary>
    /// <returns>The current health snapshot.</returns>
    PipelineHealthStatus GetHealthStatus();

    /// <summary>
    /// Gets the current operational snapshot for the pipeline.
    /// </summary>
    /// <returns>The current operational snapshot.</returns>
    PipelineOperationalSnapshot GetOperationalSnapshot();
    
    #region Eventing
    
    /// <summary>
    /// Event that is raised when a message is completed in the pipeline.
    /// </summary>
    event PipelineRecordCompletedEventHandler<T> OnPipelineRecordCompleted;

    /// <summary>
    /// Event that is raised when a record faults while traversing the pipeline.
    /// </summary>
    event PipelineRecordFaultedEventHandler<T> OnPipelineRecordFaulted;

    /// <summary>
    /// Event that is raised when a record is scheduled for retry after a transient failure.
    /// </summary>
    event PipelineRecordRetryingEventHandler<T> OnPipelineRecordRetrying;

    /// <summary>
    /// Event that is raised when a faulted record is preserved through the dead-letter path.
    /// </summary>
    event PipelineRecordDeadLetteredEventHandler<T> OnPipelineRecordDeadLettered;

    /// <summary>
    /// Event that is raised when a dead-letter write attempt fails.
    /// </summary>
    event PipelineDeadLetterWriteFailedEventHandler<T> OnPipelineDeadLetterWriteFailed;

    /// <summary>
    /// Event that is raised when pipeline saturation state changes.
    /// </summary>
    event PipelineSaturationChangedEventHandler OnSaturationChanged;

    /// <summary>
    /// Event that is raised when a publish request is rejected.
    /// </summary>
    event PipelinePublishRejectedEventHandler<T> OnPublishRejected;

    /// <summary>
    /// Event that is raised when the pipeline transitions into a faulted state.
    /// </summary>
    event PipelineFaultedEventHandler OnPipelineFaulted;

    /// <summary>
    /// Event that is raised when a distributed worker starts.
    /// </summary>
    event PipelineWorkerStartedEventHandler OnWorkerStarted;

    /// <summary>
    /// Event that is raised when partitions are assigned to the current worker.
    /// </summary>
    event PipelinePartitionsAssignedEventHandler OnPartitionsAssigned;

    /// <summary>
    /// Event that is raised when partitions are revoked from the current worker.
    /// </summary>
    event PipelinePartitionsRevokedEventHandler OnPartitionsRevoked;

    /// <summary>
    /// Event that is raised when a partition begins draining during rebalance.
    /// </summary>
    event PipelinePartitionDrainingEventHandler OnPartitionDraining;

    /// <summary>
    /// Event that is raised when a partition has fully drained.
    /// </summary>
    event PipelinePartitionDrainedEventHandler OnPartitionDrained;

    /// <summary>
    /// Event that is raised when partition execution state changes.
    /// </summary>
    event PipelinePartitionExecutionStateChangedEventHandler OnPartitionExecutionStateChanged;

    /// <summary>
    /// Event that is raised when a distributed worker is stopping.
    /// </summary>
    event PipelineWorkerStoppingEventHandler OnWorkerStopping;

    #endregion
}
