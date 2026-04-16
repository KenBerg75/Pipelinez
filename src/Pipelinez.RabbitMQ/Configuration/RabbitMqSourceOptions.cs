namespace Pipelinez.RabbitMQ.Configuration;

/// <summary>
/// Configures a RabbitMQ queue source.
/// </summary>
public sealed class RabbitMqSourceOptions
{
    /// <summary>
    /// Gets or sets the RabbitMQ connection options.
    /// </summary>
    public RabbitMqConnectionOptions Connection { get; set; } = new();

    /// <summary>
    /// Gets or sets the queue to consume.
    /// </summary>
    public RabbitMqQueueOptions Queue { get; set; } = new();

    /// <summary>
    /// Gets or sets optional topology declaration or validation options.
    /// </summary>
    public RabbitMqTopologyOptions Topology { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum number of unacknowledged deliveries for the consumer.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether QoS should be global to the channel.
    /// </summary>
    public bool GlobalQos { get; set; }

    /// <summary>
    /// Gets or sets an optional RabbitMQ consumer tag.
    /// </summary>
    public string? ConsumerTag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the consumer should be exclusive.
    /// </summary>
    public bool ExclusiveConsumer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the consumer should use no-local behavior.
    /// </summary>
    public bool NoLocal { get; set; }

    /// <summary>
    /// Gets or sets optional consumer arguments.
    /// </summary>
    public IDictionary<string, object?>? ConsumerArguments { get; set; }

    /// <summary>
    /// Gets or sets RabbitMQ.Client consumer dispatch concurrency.
    /// </summary>
    public ushort ConsumerDispatchConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets source settlement behavior.
    /// </summary>
    public RabbitMqSourceSettlementOptions Settlement { get; set; } = new();

    /// <summary>
    /// Validates the source options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public RabbitMqSourceOptions Validate()
    {
        ArgumentNullException.ThrowIfNull(Connection);
        ArgumentNullException.ThrowIfNull(Queue);
        ArgumentNullException.ThrowIfNull(Topology);
        ArgumentNullException.ThrowIfNull(Settlement);

        Connection.Validate();
        Queue.Validate();
        Topology.Validate();
        Settlement.Validate();

        if (PrefetchCount == 0)
        {
            throw new InvalidOperationException($"{nameof(PrefetchCount)} must be greater than zero.");
        }

        if (ConsumerDispatchConcurrency == 0)
        {
            throw new InvalidOperationException($"{nameof(ConsumerDispatchConcurrency)} must be greater than zero.");
        }

        if (Topology.Queue is not null &&
            !string.Equals(Topology.Queue.Name, Queue.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{nameof(Topology)} queue name must match the source queue name when both are specified.");
        }

        return this;
    }
}
