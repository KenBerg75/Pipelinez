namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// The type for Kafka Serialization
/// </summary>
public enum KafkaSerializationType
{
    /// <summary>
    /// Use the default serialization for the type
    /// </summary>
    DEFAULT,
    /// <summary>
    /// Use AVRO serialization.
    /// Note: Schema registry and Valid AVRO POCO entity required
    /// </summary>
    AVRO,
    /// <summary>
    /// Use JSON serialization for objects.
    /// Note: Schema registry required
    /// </summary>
    JSON
}
// /Deserialization)

/// <summary>
/// The type for Kafka Deserialization
/// </summary>
public enum KafkaDeserializationType
{
    /// <summary>
    /// Use the default deserialization for the type
    /// </summary>
    DEFAULT,
    /// <summary>
    /// Use AVRO deserialization.
    /// Note: Schema registry and Valid AVRO POCO entity required
    /// </summary>
    AVRO,
    /// <summary>
    /// Use JSON deserialization for objects.
    /// Note: Schema registry required
    /// </summary>
    JSON
}