namespace Pipelinez.RabbitMQ.Record;

/// <summary>
/// Defines metadata keys stamped on records consumed from RabbitMQ.
/// </summary>
public static class RabbitMqMetadataKeys
{
    /// <summary>
    /// Metadata key for the RabbitMQ queue name.
    /// </summary>
    public const string QueueName = "pipelinez.rabbitmq.queue_name";

    /// <summary>
    /// Metadata key for the RabbitMQ exchange.
    /// </summary>
    public const string Exchange = "pipelinez.rabbitmq.exchange";

    /// <summary>
    /// Metadata key for the RabbitMQ routing key.
    /// </summary>
    public const string RoutingKey = "pipelinez.rabbitmq.routing_key";

    /// <summary>
    /// Metadata key for the RabbitMQ consumer tag.
    /// </summary>
    public const string ConsumerTag = "pipelinez.rabbitmq.consumer_tag";

    /// <summary>
    /// Metadata key for the RabbitMQ delivery tag.
    /// </summary>
    public const string DeliveryTag = "pipelinez.rabbitmq.delivery_tag";

    /// <summary>
    /// Metadata key for the RabbitMQ redelivered flag.
    /// </summary>
    public const string Redelivered = "pipelinez.rabbitmq.redelivered";

    /// <summary>
    /// Metadata key for the RabbitMQ message id.
    /// </summary>
    public const string MessageId = "pipelinez.rabbitmq.message_id";

    /// <summary>
    /// Metadata key for the RabbitMQ correlation id.
    /// </summary>
    public const string CorrelationId = "pipelinez.rabbitmq.correlation_id";

    /// <summary>
    /// Metadata key for the RabbitMQ content type.
    /// </summary>
    public const string ContentType = "pipelinez.rabbitmq.content_type";

    /// <summary>
    /// Metadata key for the RabbitMQ content encoding.
    /// </summary>
    public const string ContentEncoding = "pipelinez.rabbitmq.content_encoding";

    /// <summary>
    /// Metadata key for the RabbitMQ delivery mode.
    /// </summary>
    public const string DeliveryMode = "pipelinez.rabbitmq.delivery_mode";

    /// <summary>
    /// Metadata key for the RabbitMQ priority.
    /// </summary>
    public const string Priority = "pipelinez.rabbitmq.priority";

    /// <summary>
    /// Metadata key for the RabbitMQ timestamp.
    /// </summary>
    public const string Timestamp = "pipelinez.rabbitmq.timestamp";

    /// <summary>
    /// Metadata key for the RabbitMQ expiration property.
    /// </summary>
    public const string Expiration = "pipelinez.rabbitmq.expiration";

    /// <summary>
    /// Metadata key for the RabbitMQ type property.
    /// </summary>
    public const string Type = "pipelinez.rabbitmq.type";

    /// <summary>
    /// Metadata key for the RabbitMQ user id property.
    /// </summary>
    public const string UserId = "pipelinez.rabbitmq.user_id";

    /// <summary>
    /// Metadata key for the RabbitMQ app id property.
    /// </summary>
    public const string AppId = "pipelinez.rabbitmq.app_id";

    /// <summary>
    /// Metadata key for the RabbitMQ cluster id property.
    /// </summary>
    public const string ClusterId = "pipelinez.rabbitmq.cluster_id";
}
