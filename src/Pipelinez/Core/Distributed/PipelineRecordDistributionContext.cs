using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

/// <summary>
/// Captures the distributed execution context associated with a single record.
/// </summary>
public sealed class PipelineRecordDistributionContext
{
    /// <summary>
    /// Initializes a new record distribution context.
    /// </summary>
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

    /// <summary>
    /// Gets the host instance identifier that processed the record.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Gets the worker identifier that processed the record.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the transport name, if the source provided one.
    /// </summary>
    public string? TransportName { get; }

    /// <summary>
    /// Gets the logical lease identifier, if the source provided one.
    /// </summary>
    public string? LeaseId { get; }

    /// <summary>
    /// Gets the logical partition key, if the source provided one.
    /// </summary>
    public string? PartitionKey { get; }

    /// <summary>
    /// Gets the numeric partition identifier, if the source provided one.
    /// </summary>
    public int? PartitionId { get; }

    /// <summary>
    /// Gets the transport offset, if the source provided one.
    /// </summary>
    public long? Offset { get; }
}
