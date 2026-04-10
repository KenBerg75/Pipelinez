using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

/// <summary>
/// Represents the distributed execution status exposed through pipeline status APIs.
/// </summary>
public sealed class PipelineDistributedStatus
{
    /// <summary>
    /// Initializes a new distributed status snapshot.
    /// </summary>
    public PipelineDistributedStatus(
        PipelineExecutionMode executionMode,
        string instanceId,
        string workerId,
        IReadOnlyList<PipelinePartitionLease>? ownedPartitions = null,
        IReadOnlyList<PipelinePartitionExecutionState>? partitionExecution = null)
    {
        ExecutionMode = executionMode;
        InstanceId = Guard.Against.NullOrWhiteSpace(instanceId, nameof(instanceId));
        WorkerId = Guard.Against.NullOrWhiteSpace(workerId, nameof(workerId));
        OwnedPartitions = ownedPartitions?.ToArray() ?? Array.Empty<PipelinePartitionLease>();
        PartitionExecution = partitionExecution?.ToArray() ?? Array.Empty<PipelinePartitionExecutionState>();
    }

    /// <summary>
    /// Gets the execution mode currently used by the pipeline.
    /// </summary>
    public PipelineExecutionMode ExecutionMode { get; }

    /// <summary>
    /// Gets the host instance identifier.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Gets the worker identifier.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the partitions currently owned by the worker.
    /// </summary>
    public IReadOnlyList<PipelinePartitionLease> OwnedPartitions { get; }

    /// <summary>
    /// Gets the current execution state of owned partitions.
    /// </summary>
    public IReadOnlyList<PipelinePartitionExecutionState> PartitionExecution { get; }
}
