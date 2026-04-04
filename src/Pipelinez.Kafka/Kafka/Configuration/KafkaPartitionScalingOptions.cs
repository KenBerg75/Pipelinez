using Ardalis.GuardClauses;

namespace Pipelinez.Kafka.Configuration;

public sealed class KafkaPartitionScalingOptions
{
    public KafkaPartitionExecutionMode ExecutionMode { get; init; } =
        KafkaPartitionExecutionMode.PreservePartitionOrder;

    public int MaxConcurrentPartitions { get; init; } = int.MaxValue;

    public int MaxInFlightPerPartition { get; init; } = 1;

    public KafkaPartitionRebalanceMode RebalanceMode { get; init; } =
        KafkaPartitionRebalanceMode.DrainAndYield;

    public bool EmitPartitionExecutionEvents { get; init; } = true;

    public KafkaPartitionScalingOptions Validate()
    {
        Guard.Against.NegativeOrZero(MaxConcurrentPartitions, nameof(MaxConcurrentPartitions));
        Guard.Against.NegativeOrZero(MaxInFlightPerPartition, nameof(MaxInFlightPerPartition));

        if (ExecutionMode != KafkaPartitionExecutionMode.RelaxOrderingWithinPartition &&
            MaxInFlightPerPartition != 1)
        {
            throw new InvalidOperationException(
                $"Kafka partition execution mode '{ExecutionMode}' requires MaxInFlightPerPartition to be 1.");
        }

        return this;
    }
}
