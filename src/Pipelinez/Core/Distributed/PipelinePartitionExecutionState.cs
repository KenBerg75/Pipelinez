using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

/// <summary>
/// Describes the current execution state of a single owned partition.
/// </summary>
public sealed class PipelinePartitionExecutionState
{
    /// <summary>
    /// Initializes a new partition execution state snapshot.
    /// </summary>
    public PipelinePartitionExecutionState(
        string leaseId,
        string partitionKey,
        int? partitionId,
        bool isAssigned,
        bool isDraining,
        int inFlightCount,
        long? highestCompletedOffset)
    {
        LeaseId = Guard.Against.NullOrWhiteSpace(leaseId, nameof(leaseId));
        PartitionKey = Guard.Against.NullOrWhiteSpace(partitionKey, nameof(partitionKey));
        PartitionId = partitionId;
        IsAssigned = isAssigned;
        IsDraining = isDraining;
        InFlightCount = Guard.Against.Negative(inFlightCount, nameof(inFlightCount));
        HighestCompletedOffset = highestCompletedOffset;
    }

    /// <summary>
    /// Gets the logical lease identifier.
    /// </summary>
    public string LeaseId { get; }

    /// <summary>
    /// Gets the logical partition key.
    /// </summary>
    public string PartitionKey { get; }

    /// <summary>
    /// Gets the numeric partition identifier, if one exists.
    /// </summary>
    public int? PartitionId { get; }

    /// <summary>
    /// Gets a value indicating whether the partition is currently assigned to this worker.
    /// </summary>
    public bool IsAssigned { get; }

    /// <summary>
    /// Gets a value indicating whether the partition is draining during rebalance or shutdown.
    /// </summary>
    public bool IsDraining { get; }

    /// <summary>
    /// Gets the number of in-flight records for the partition.
    /// </summary>
    public int InFlightCount { get; }

    /// <summary>
    /// Gets the highest completed offset observed for the partition, if one exists.
    /// </summary>
    public long? HighestCompletedOffset { get; }
}
