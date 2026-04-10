using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

/// <summary>
/// Represents transport-owned work currently assigned to a pipeline worker.
/// </summary>
public sealed class PipelinePartitionLease
{
    /// <summary>
    /// Initializes a new partition lease description.
    /// </summary>
    /// <param name="leaseId">The logical lease identifier.</param>
    /// <param name="transportName">The name of the transport that owns the partition.</param>
    /// <param name="partitionKey">The logical partition key.</param>
    /// <param name="instanceId">The host instance identifier.</param>
    /// <param name="workerId">The worker identifier.</param>
    /// <param name="partitionId">The numeric partition identifier, if one exists.</param>
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

    /// <summary>
    /// Gets the logical lease identifier.
    /// </summary>
    public string LeaseId { get; }

    /// <summary>
    /// Gets the transport name that owns the partition.
    /// </summary>
    public string TransportName { get; }

    /// <summary>
    /// Gets the logical partition key.
    /// </summary>
    public string PartitionKey { get; }

    /// <summary>
    /// Gets the instance identifier of the host that owns the partition.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Gets the worker identifier that owns the partition.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the numeric partition identifier, if the transport exposes one.
    /// </summary>
    public int? PartitionId { get; }
}
