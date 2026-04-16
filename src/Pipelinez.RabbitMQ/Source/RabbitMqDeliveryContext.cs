using RabbitMQ.Client;

namespace Pipelinez.RabbitMQ.Source;

/// <summary>
/// Describes a RabbitMQ delivery being mapped into a Pipelinez record.
/// </summary>
public sealed class RabbitMqDeliveryContext
{
    /// <summary>
    /// Gets the RabbitMQ consumer tag.
    /// </summary>
    public required string ConsumerTag { get; init; }

    /// <summary>
    /// Gets the RabbitMQ channel delivery tag.
    /// </summary>
    public required ulong DeliveryTag { get; init; }

    /// <summary>
    /// Gets a value indicating whether RabbitMQ marked this delivery as redelivered.
    /// </summary>
    public required bool Redelivered { get; init; }

    /// <summary>
    /// Gets the exchange from which the message was delivered.
    /// </summary>
    public required string Exchange { get; init; }

    /// <summary>
    /// Gets the routing key used for the delivery.
    /// </summary>
    public required string RoutingKey { get; init; }

    /// <summary>
    /// Gets a copied body buffer for the delivery.
    /// </summary>
    public required ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>
    /// Gets the RabbitMQ basic properties for the delivery.
    /// </summary>
    public IReadOnlyBasicProperties? Properties { get; init; }
}
