namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// Configuration object for a Kafka Destination
/// </summary>
public class KafkaDestinationOptions : KafkaOptions
{
    public string TopicName { get; set; } = string.Empty;
    public KafkaSchemaRegistryOptions? Schema { get; set; } = new KafkaSchemaRegistryOptions();
}