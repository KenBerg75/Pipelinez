namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// Configuration object for a Kafka Destination
/// </summary>
public class KafkaDestinationOptions : KafkaOptions
{
    /// <summary>
    /// Gets or sets the Kafka topic to publish records to.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional schema registry configuration used by serializers for the destination topic.
    /// </summary>
    public KafkaSchemaRegistryOptions? Schema { get; set; } = new KafkaSchemaRegistryOptions();
}
