namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// Configuration block for a Kafka Source
/// </summary>
public class KafkaSourceOptions : KafkaOptions
{
    public string TopicName { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;
    public bool StartOffsetFromBeginning { get; set; } = false;
    public KafkaSchemaRegistryOptions Schema { get; set; } = new KafkaSchemaRegistryOptions();
}