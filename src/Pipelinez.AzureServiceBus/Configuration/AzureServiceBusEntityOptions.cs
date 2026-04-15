namespace Pipelinez.AzureServiceBus.Configuration;

/// <summary>
/// Identifies the Azure Service Bus entity used by a source or destination.
/// </summary>
public sealed class AzureServiceBusEntityOptions
{
    /// <summary>
    /// Gets or sets the entity kind.
    /// </summary>
    public AzureServiceBusEntityKind EntityKind { get; set; }

    /// <summary>
    /// Gets or sets the queue name when <see cref="EntityKind" /> is <see cref="AzureServiceBusEntityKind.Queue" />.
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>
    /// Gets or sets the topic name when <see cref="EntityKind" /> is <see cref="AzureServiceBusEntityKind.Topic" /> or <see cref="AzureServiceBusEntityKind.TopicSubscription" />.
    /// </summary>
    public string? TopicName { get; set; }

    /// <summary>
    /// Gets or sets the subscription name when <see cref="EntityKind" /> is <see cref="AzureServiceBusEntityKind.TopicSubscription" />.
    /// </summary>
    public string? SubscriptionName { get; set; }

    /// <summary>
    /// Creates queue entity options.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <returns>Queue entity options.</returns>
    public static AzureServiceBusEntityOptions ForQueue(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        return new AzureServiceBusEntityOptions
        {
            EntityKind = AzureServiceBusEntityKind.Queue,
            QueueName = queueName
        };
    }

    /// <summary>
    /// Creates topic entity options.
    /// </summary>
    /// <param name="topicName">The topic name.</param>
    /// <returns>Topic entity options.</returns>
    public static AzureServiceBusEntityOptions ForTopic(string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        return new AzureServiceBusEntityOptions
        {
            EntityKind = AzureServiceBusEntityKind.Topic,
            TopicName = topicName
        };
    }

    /// <summary>
    /// Creates topic subscription entity options.
    /// </summary>
    /// <param name="topicName">The topic name.</param>
    /// <param name="subscriptionName">The subscription name.</param>
    /// <returns>Topic subscription entity options.</returns>
    public static AzureServiceBusEntityOptions ForTopicSubscription(string topicName, string subscriptionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        return new AzureServiceBusEntityOptions
        {
            EntityKind = AzureServiceBusEntityKind.TopicSubscription,
            TopicName = topicName,
            SubscriptionName = subscriptionName
        };
    }

    /// <summary>
    /// Validates the entity for source usage and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public AzureServiceBusEntityOptions ValidateForSource()
    {
        return EntityKind switch
        {
            AzureServiceBusEntityKind.Queue => ValidateQueue(),
            AzureServiceBusEntityKind.TopicSubscription => ValidateTopicSubscription(),
            AzureServiceBusEntityKind.Topic => throw new InvalidOperationException(
                "Azure Service Bus sources require a queue or topic subscription. A bare topic cannot be consumed."),
            _ => throw new InvalidOperationException($"Unsupported Azure Service Bus entity kind '{EntityKind}'.")
        };
    }

    /// <summary>
    /// Validates the entity for destination usage and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public AzureServiceBusEntityOptions ValidateForDestination()
    {
        return EntityKind switch
        {
            AzureServiceBusEntityKind.Queue => ValidateQueue(),
            AzureServiceBusEntityKind.Topic => ValidateTopic(),
            AzureServiceBusEntityKind.TopicSubscription => throw new InvalidOperationException(
                "Azure Service Bus destinations require a queue or topic. A subscription cannot receive direct sends."),
            _ => throw new InvalidOperationException($"Unsupported Azure Service Bus entity kind '{EntityKind}'.")
        };
    }

    internal string GetEntityPath()
    {
        return EntityKind switch
        {
            AzureServiceBusEntityKind.Queue => QueueName!,
            AzureServiceBusEntityKind.Topic => TopicName!,
            AzureServiceBusEntityKind.TopicSubscription => $"{TopicName!}/Subscriptions/{SubscriptionName!}",
            _ => throw new InvalidOperationException($"Unsupported Azure Service Bus entity kind '{EntityKind}'.")
        };
    }

    internal string GetPartitionKey()
    {
        return EntityKind == AzureServiceBusEntityKind.TopicSubscription
            ? $"{TopicName!}/{SubscriptionName!}"
            : QueueName ?? TopicName ?? string.Empty;
    }

    private AzureServiceBusEntityOptions ValidateQueue()
    {
        if (string.IsNullOrWhiteSpace(QueueName))
        {
            throw new InvalidOperationException($"{nameof(QueueName)} is required for queue entities.");
        }

        if (!string.IsNullOrWhiteSpace(TopicName) || !string.IsNullOrWhiteSpace(SubscriptionName))
        {
            throw new InvalidOperationException("Queue entities cannot also specify topic or subscription names.");
        }

        return this;
    }

    private AzureServiceBusEntityOptions ValidateTopic()
    {
        if (string.IsNullOrWhiteSpace(TopicName))
        {
            throw new InvalidOperationException($"{nameof(TopicName)} is required for topic entities.");
        }

        if (!string.IsNullOrWhiteSpace(QueueName) || !string.IsNullOrWhiteSpace(SubscriptionName))
        {
            throw new InvalidOperationException("Topic entities cannot also specify queue or subscription names.");
        }

        return this;
    }

    private AzureServiceBusEntityOptions ValidateTopicSubscription()
    {
        if (string.IsNullOrWhiteSpace(TopicName))
        {
            throw new InvalidOperationException($"{nameof(TopicName)} is required for topic subscription entities.");
        }

        if (string.IsNullOrWhiteSpace(SubscriptionName))
        {
            throw new InvalidOperationException($"{nameof(SubscriptionName)} is required for topic subscription entities.");
        }

        if (!string.IsNullOrWhiteSpace(QueueName))
        {
            throw new InvalidOperationException("Topic subscription entities cannot also specify a queue name.");
        }

        return this;
    }
}
