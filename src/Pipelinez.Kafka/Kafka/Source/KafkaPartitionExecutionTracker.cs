using Confluent.Kafka;
using Pipelinez.Core.Distributed;

namespace Pipelinez.Kafka.Source;

internal sealed class KafkaPartitionExecutionTracker
{
    public KafkaPartitionExecutionTracker(TopicPartition topicPartition, PipelinePartitionLease lease)
    {
        TopicPartition = topicPartition;
        Lease = lease;
    }

    public TopicPartition TopicPartition { get; }

    public PipelinePartitionLease Lease { get; }

    public bool IsAssigned { get; set; } = true;

    public bool IsDraining { get; set; }

    public bool IsPaused { get; set; }

    public int InFlightCount { get; set; }

    public long? HighestCompletedOffset { get; set; }

    public long? NextCommitOffset { get; set; }

    public SortedSet<long> CompletedOffsets { get; } = new();

    public PipelinePartitionExecutionState ToPublicState()
    {
        return new PipelinePartitionExecutionState(
            Lease.LeaseId,
            Lease.PartitionKey,
            Lease.PartitionId,
            IsAssigned,
            IsDraining,
            InFlightCount,
            HighestCompletedOffset);
    }
}
