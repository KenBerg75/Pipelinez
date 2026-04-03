using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

public sealed class PipelinePartitionLease
{
    public PipelinePartitionLease(
        string leaseId,
        string transportName,
        string partitionKey,
        string instanceId,
        string workerId,
        int? partitionId = null)
    {
        LeaseId = Guard.Against.NullOrWhiteSpace(leaseId, nameof(leaseId));
        TransportName = Guard.Against.NullOrWhiteSpace(transportName, nameof(transportName));
        PartitionKey = Guard.Against.NullOrWhiteSpace(partitionKey, nameof(partitionKey));
        InstanceId = Guard.Against.NullOrWhiteSpace(instanceId, nameof(instanceId));
        WorkerId = Guard.Against.NullOrWhiteSpace(workerId, nameof(workerId));
        PartitionId = partitionId;
    }

    public string LeaseId { get; }

    public string TransportName { get; }

    public string PartitionKey { get; }

    public string InstanceId { get; }

    public string WorkerId { get; }

    public int? PartitionId { get; }
}
