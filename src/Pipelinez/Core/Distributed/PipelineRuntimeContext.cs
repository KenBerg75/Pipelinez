using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

public sealed class PipelineRuntimeContext
{
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

    public string PipelineName { get; }

    public PipelineExecutionMode ExecutionMode { get; }

    public string InstanceId { get; }

    public string WorkerId { get; }

    public IReadOnlyList<PipelinePartitionLease> OwnedPartitions { get; }

    public IReadOnlyList<PipelinePartitionExecutionState> PartitionExecution { get; }
}
