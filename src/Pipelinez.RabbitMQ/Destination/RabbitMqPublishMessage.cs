using RabbitMQ.Client;

namespace Pipelinez.RabbitMQ.Destination;

/// <summary>
/// Describes a RabbitMQ message to publish.
/// </summary>
public sealed class RabbitMqPublishMessage
{
    /// <summary>
    /// Gets or sets an optional exchange override. When omitted, destination options provide the exchange.
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// Gets or sets an optional routing-key override. When omitted, destination options provide the routing key.
    /// </summary>
    public string? RoutingKey { get; init; }

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>
    /// Gets or sets the RabbitMQ basic properties to publish.
    /// </summary>
    public BasicProperties? Properties { get; init; }

    /// <summary>
    /// Gets or sets an optional mandatory flag override.
    /// </summary>
    public bool? Mandatory { get; init; }

    /// <summary>
    /// Creates a publish message with the supplied body and optional routing information.
    /// </summary>
    /// <param name="body">The message body.</param>
    /// <param name="exchange">The optional exchange override.</param>
    /// <param name="routingKey">The optional routing-key override.</param>
    /// <param name="properties">The optional message properties.</param>
    /// <param name="mandatory">The optional mandatory flag override.</param>
    /// <returns>The publish message.</returns>
    public static RabbitMqPublishMessage Create(
        ReadOnlyMemory<byte> body,
        string? exchange = null,
        string? routingKey = null,
        BasicProperties? properties = null,
        bool? mandatory = null)
    {
        return new RabbitMqPublishMessage
        {
            Body = body,
            Exchange = exchange,
            RoutingKey = routingKey,
            Properties = properties,
            Mandatory = mandatory
        };
    }
}
