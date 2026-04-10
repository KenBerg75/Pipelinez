namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// Defines how a Kafka source should behave when a partition rebalance revokes owned partitions.
/// </summary>
public enum KafkaPartitionRebalanceMode
{
    /// <summary>
    /// Stops admitting new work for the partition and waits for in-flight records to drain before yielding ownership.
    /// </summary>
    DrainAndYield,

    /// <summary>
    /// Stops admitting new work immediately and yields ownership as soon as active work is no longer accepted.
    /// </summary>
    StopAcceptingNewWorkAndYield
}
