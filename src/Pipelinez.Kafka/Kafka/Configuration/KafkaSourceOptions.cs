namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// Configuration block for a Kafka Source
/// </summary>
public class KafkaSourceOptions : KafkaOptions
{
    /// <summary>
    /// Gets or sets the Kafka topic to consume from.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Kafka consumer group used by the source.
    /// </summary>
    public string ConsumerGroup { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether a brand new consumer group should begin from the start of the topic.
    /// </summary>
    public bool StartOffsetFromBeginning { get; set; } = false;

    /// <summary>
    /// Gets or sets the automatic offset commit interval in milliseconds when broker auto-commit is enabled.
    /// </summary>
    public int? AutoCommitIntervalMs { get; set; }

    /// <summary>
    /// Gets or sets optional schema registry configuration used by Kafka deserializers.
    /// </summary>
    public KafkaSchemaRegistryOptions Schema { get; set; } = new KafkaSchemaRegistryOptions();

    /// <summary>
    /// Gets or sets partition-aware scaling options for distributed Kafka consumption.
    /// </summary>
    public KafkaPartitionScalingOptions PartitionScaling { get; set; } = new KafkaPartitionScalingOptions();
}
