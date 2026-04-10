namespace Pipelinez.Kafka.Record;

/// <summary>
/// Defines metadata keys used for Kafka source records.
/// </summary>
public static class KafkaMetadataKeys
{
    /// <summary>
    /// Metadata key that stores the source Kafka topic name.
    /// </summary>
    public static string SOURCE_TOPIC_NAME = "kafka.source.topic.name";

    /// <summary>
    /// Metadata key that stores the source Kafka offset.
    /// </summary>
    public static string SOURCE_OFFSET = "kafka.source.offset";

    /// <summary>
    /// Metadata key that stores the source Kafka partition identifier.
    /// </summary>
    public static string SOURCE_PARTITION = "kafka.source.partition";
}
