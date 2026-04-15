namespace Pipelinez.AzureServiceBus.Record;

/// <summary>
/// Defines metadata keys stamped on records consumed from Azure Service Bus.
/// </summary>
public static class AzureServiceBusMetadataKeys
{
    /// <summary>
    /// Metadata key for the source entity kind.
    /// </summary>
    public const string EntityKind = "pipelinez.asb.entity_kind";

    /// <summary>
    /// Metadata key for the source queue name.
    /// </summary>
    public const string QueueName = "pipelinez.asb.queue_name";

    /// <summary>
    /// Metadata key for the source topic name.
    /// </summary>
    public const string TopicName = "pipelinez.asb.topic_name";

    /// <summary>
    /// Metadata key for the source subscription name.
    /// </summary>
    public const string SubscriptionName = "pipelinez.asb.subscription_name";

    /// <summary>
    /// Metadata key for the Service Bus message id.
    /// </summary>
    public const string MessageId = "pipelinez.asb.message_id";

    /// <summary>
    /// Metadata key for the Service Bus sequence number.
    /// </summary>
    public const string SequenceNumber = "pipelinez.asb.sequence_number";

    /// <summary>
    /// Metadata key for the Service Bus enqueued timestamp.
    /// </summary>
    public const string EnqueuedTimeUtc = "pipelinez.asb.enqueued_time_utc";

    /// <summary>
    /// Metadata key for the Service Bus delivery count.
    /// </summary>
    public const string DeliveryCount = "pipelinez.asb.delivery_count";

    /// <summary>
    /// Metadata key for the Service Bus lock token.
    /// </summary>
    public const string LockToken = "pipelinez.asb.lock_token";

    /// <summary>
    /// Metadata key for the Service Bus locked-until timestamp.
    /// </summary>
    public const string LockedUntilUtc = "pipelinez.asb.locked_until_utc";

    /// <summary>
    /// Metadata key for the Service Bus session id.
    /// </summary>
    public const string SessionId = "pipelinez.asb.session_id";

    /// <summary>
    /// Metadata key for the Service Bus correlation id.
    /// </summary>
    public const string CorrelationId = "pipelinez.asb.correlation_id";

    /// <summary>
    /// Metadata key for the Service Bus subject.
    /// </summary>
    public const string Subject = "pipelinez.asb.subject";

    /// <summary>
    /// Metadata key for the Service Bus content type.
    /// </summary>
    public const string ContentType = "pipelinez.asb.content_type";

    /// <summary>
    /// Metadata key for the Service Bus reply-to property.
    /// </summary>
    public const string ReplyTo = "pipelinez.asb.reply_to";
}
