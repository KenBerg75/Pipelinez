using Pipelinez.Core.Distributed;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Operational;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;
using Pipelinez.Core.Status;

namespace Pipelinez.Core;

public interface IPipeline<T> where T : PipelineRecord
{
    /// <summary>
    /// Starts up the pipeline
    /// </summary>
    /// <param name="cancellationToken">A token allowing the cancellation of the pipeline</param>
    /// <returns></returns>
    Task StartPipelineAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Publishes a record into the pipeline. Acts as a manual source for the pipeline.
    /// </summary>
    /// <returns></returns>
    Task PublishAsync(T record);

    /// <summary>
    /// Publishes a record into the pipeline with explicit flow-control behavior.
    /// </summary>
    Task<PipelinePublishResult> PublishAsync(T record, PipelinePublishOptions options);

    /// <summary>
    /// Completes and shuts down the pipeline.
    /// </summary>
    Task CompleteAsync();

    /// <summary>Gets a <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation and completion of the pipeline.</summary>
    /// <returns>The task.</returns>
    Task Completion { get; }

    PipelineStatus GetStatus();

    PipelineRuntimeContext GetRuntimeContext();

    PipelinePerformanceSnapshot GetPerformanceSnapshot();

    PipelineHealthStatus GetHealthStatus();

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
