namespace Pipelinez.AzureServiceBus.Configuration;

/// <summary>
/// Identifies the kind of Azure Service Bus entity used by a transport component.
/// </summary>
public enum AzureServiceBusEntityKind
{
    /// <summary>
    /// The entity is a queue.
    /// </summary>
    Queue,

    /// <summary>
    /// The entity is a topic.
    /// </summary>
    Topic,

    /// <summary>
    /// The entity is a topic subscription.
    /// </summary>
    TopicSubscription
}
