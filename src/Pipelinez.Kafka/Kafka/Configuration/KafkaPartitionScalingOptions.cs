using Ardalis.GuardClauses;

namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// Configures partition-aware scheduling and rebalance behavior for Kafka sources.
/// </summary>
public sealed class KafkaPartitionScalingOptions
{
    /// <summary>
    /// Gets or sets how records may execute across and within assigned partitions.
    /// </summary>
    public KafkaPartitionExecutionMode ExecutionMode { get; init; } =
        KafkaPartitionExecutionMode.PreservePartitionOrder;

    /// <summary>
    /// Gets or sets the maximum number of partitions that may have active in-flight work at one time.
    /// </summary>
    public int MaxConcurrentPartitions { get; init; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the maximum number of in-flight records allowed within a single partition.
    /// </summary>
    public int MaxInFlightPerPartition { get; init; } = 1;

    /// <summary>
    /// Gets or sets how partition revocation should be coordinated during rebalances.
    /// </summary>
    public KafkaPartitionRebalanceMode RebalanceMode { get; init; } =
        KafkaPartitionRebalanceMode.DrainAndYield;

    /// <summary>
    /// Gets or sets a value indicating whether partition execution lifecycle events should be raised.
    /// </summary>
    public bool EmitPartitionExecutionEvents { get; init; } = true;

    /// <summary>
    /// Validates the configured scaling options and returns the same instance when they are valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the execution mode requires ordered partition processing but <see cref="MaxInFlightPerPartition"/> allows more than one record.
    /// </exception>
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
