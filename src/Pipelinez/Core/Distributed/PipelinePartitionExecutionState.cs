using Ardalis.GuardClauses;

namespace Pipelinez.Core.Distributed;

public sealed class PipelinePartitionExecutionState
{
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

    public string LeaseId { get; }

    public string PartitionKey { get; }

    public int? PartitionId { get; }

    public bool IsAssigned { get; }

    public bool IsDraining { get; }

    public int InFlightCount { get; }

    public long? HighestCompletedOffset { get; }
}
