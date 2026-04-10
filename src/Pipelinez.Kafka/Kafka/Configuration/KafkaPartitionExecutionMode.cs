namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// Defines how a Kafka source is allowed to execute work across and within assigned partitions.
/// </summary>
public enum KafkaPartitionExecutionMode
{
    /// <summary>
    /// Processes at most one record at a time per partition so partition ordering is preserved.
    /// </summary>
    PreservePartitionOrder,

    /// <summary>
    /// Allows different partitions to execute concurrently while preserving ordering within each partition.
    /// </summary>
    ParallelizeAcrossPartitions,

    /// <summary>
    /// Allows multiple records from the same partition to execute concurrently, which can relax ordering guarantees.
    /// </summary>
    RelaxOrderingWithinPartition
}
