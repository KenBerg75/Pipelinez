namespace Pipelinez.RabbitMQ.Configuration;

/// <summary>
/// Configures a RabbitMQ destination.
/// </summary>
public class RabbitMqDestinationOptions
{
    /// <summary>
    /// Gets or sets the RabbitMQ connection options.
    /// </summary>
    public RabbitMqConnectionOptions Connection { get; set; } = new();

    /// <summary>
    /// Gets or sets the exchange to publish to. Use an empty string for the default exchange.
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default routing key.
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional topology declaration or validation options.
    /// </summary>
    public RabbitMqTopologyOptions Topology { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether mandatory publishing is enabled.
    /// </summary>
    public bool Mandatory { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether publisher confirms are enabled.
    /// </summary>
    public bool UsePublisherConfirms { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout used for publisher confirmations.
    /// </summary>
    public TimeSpan PublisherConfirmTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether messages should be persistent by default.
    /// </summary>
    public bool PersistentMessagesByDefault { get; set; } = true;

    /// <summary>
    /// Validates the destination options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public RabbitMqDestinationOptions Validate()
    {
        ArgumentNullException.ThrowIfNull(Connection);
        ArgumentNullException.ThrowIfNull(Topology);

        Connection.Validate();
        Topology.Validate();

        if (string.IsNullOrWhiteSpace(RoutingKey) && string.IsNullOrWhiteSpace(Exchange))
        {
            throw new InvalidOperationException(
                $"{nameof(RoutingKey)} is required when publishing to the default exchange.");
        }

        if (UsePublisherConfirms && PublisherConfirmTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(PublisherConfirmTimeout)} must be greater than zero when publisher confirms are enabled.");
        }

        return this;
    }
}
