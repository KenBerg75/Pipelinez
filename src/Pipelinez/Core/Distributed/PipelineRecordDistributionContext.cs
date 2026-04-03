using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

public sealed class PipelineRecordDistributionContext
{
    public PipelineRecordDistributionContext(
        string instanceId,
        string workerId,
        string? transportName,
        string? leaseId,
        string? partitionKey,
        int? partitionId,
        long? offset)
    {
        InstanceId = Guard.Against.NullOrWhiteSpace(instanceId, nameof(instanceId));
        WorkerId = Guard.Against.NullOrWhiteSpace(workerId, nameof(workerId));
        TransportName = transportName;
        LeaseId = leaseId;
        PartitionKey = partitionKey;
        PartitionId = partitionId;
        Offset = offset;
    }

    public string InstanceId { get; }

    public string WorkerId { get; }

    public string? TransportName { get; }

    public string? LeaseId { get; }

    public string? PartitionKey { get; }

    public int? PartitionId { get; }

    public long? Offset { get; }
}
