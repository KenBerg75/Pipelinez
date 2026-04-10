using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

/// <summary>
/// Describes the current runtime identity and owned work for a running pipeline.
/// </summary>
public sealed class PipelineRuntimeContext
{
    /// <summary>
    /// Initializes a new runtime context snapshot.
    /// </summary>
    public PipelineRuntimeContext(
        string pipelineName,
        PipelineExecutionMode executionMode,
        string instanceId,
        string workerId,
        IReadOnlyList<PipelinePartitionLease>? ownedPartitions = null,
        IReadOnlyList<PipelinePartitionExecutionState>? partitionExecution = null)
    {
        PipelineName = Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));
        ExecutionMode = executionMode;
        InstanceId = Guard.Against.NullOrWhiteSpace(instanceId, nameof(instanceId));
        WorkerId = Guard.Against.NullOrWhiteSpace(workerId, nameof(workerId));
        OwnedPartitions = ownedPartitions?.ToArray() ?? Array.Empty<PipelinePartitionLease>();
        PartitionExecution = partitionExecution?.ToArray() ?? Array.Empty<PipelinePartitionExecutionState>();
    }

    /// <summary>
    /// Gets the logical pipeline name.
    /// </summary>
    public string PipelineName { get; }

    /// <summary>
    /// Gets the configured execution mode.
    /// </summary>
    public PipelineExecutionMode ExecutionMode { get; }

    /// <summary>
    /// Gets the current host instance identifier.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Gets the current worker identifier.
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
