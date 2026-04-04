namespace Pipelinez.Kafka.Configuration;

public enum KafkaPartitionExecutionMode
{
    PreservePartitionOrder,
    ParallelizeAcrossPartitions,
    RelaxOrderingWithinPartition
}
