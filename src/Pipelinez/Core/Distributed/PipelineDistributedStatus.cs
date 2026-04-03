using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

public sealed class PipelineDistributedStatus
{
    public PipelineDistributedStatus(
        PipelineExecutionMode executionMode,
        string instanceId,
        string workerId,
        IReadOnlyList<PipelinePartitionLease>? ownedPartitions = null)
    {
        ExecutionMode = executionMode;
        InstanceId = Guard.Against.NullOrWhiteSpace(instanceId, nameof(instanceId));
        WorkerId = Guard.Against.NullOrWhiteSpace(workerId, nameof(workerId));
        OwnedPartitions = ownedPartitions?.ToArray() ?? Array.Empty<PipelinePartitionLease>();
    }

    public PipelineExecutionMode ExecutionMode { get; }

    public string InstanceId { get; }

    public string WorkerId { get; }

    public IReadOnlyList<PipelinePartitionLease> OwnedPartitions { get; }
}
