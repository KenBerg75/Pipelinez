namespace Pipelinez.RabbitMQ.Configuration;

/// <summary>
/// Configures optional RabbitMQ topology declaration or validation.
/// </summary>
public sealed class RabbitMqTopologyOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Pipelinez should declare the configured exchange.
    /// </summary>
    public bool DeclareExchange { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Pipelinez should declare the configured queue.
    /// </summary>
    public bool DeclareQueue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Pipelinez should bind the queue to the exchange.
    /// </summary>
    public bool BindQueue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether topology checks should use passive declarations only.
    /// </summary>
    public bool PassiveDeclareOnly { get; set; }

    /// <summary>
    /// Gets or sets the optional exchange declaration options.
    /// </summary>
    public RabbitMqExchangeOptions? Exchange { get; set; }

    /// <summary>
    /// Gets or sets the optional queue declaration options.
    /// </summary>
    public RabbitMqQueueOptions? Queue { get; set; }

    /// <summary>
    /// Gets or sets the routing key used when binding the queue.
    /// </summary>
    public string? BindingRoutingKey { get; set; }

    /// <summary>
    /// Gets or sets binding arguments.
    /// </summary>
    public IDictionary<string, object?>? BindingArguments { get; set; }

    /// <summary>
    /// Validates the topology options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public RabbitMqTopologyOptions Validate()
    {
        Exchange?.Validate();
        Queue?.Validate();

        if (DeclareExchange && Exchange is null)
        {
            throw new InvalidOperationException($"{nameof(Exchange)} is required when {nameof(DeclareExchange)} is enabled.");
        }

        if (DeclareQueue && Queue is null)
        {
            throw new InvalidOperationException($"{nameof(Queue)} is required when {nameof(DeclareQueue)} is enabled.");
        }

        if (BindQueue && (Queue is null || Exchange is null))
        {
            throw new InvalidOperationException($"{nameof(Queue)} and {nameof(Exchange)} are required when {nameof(BindQueue)} is enabled.");
        }

        return this;
    }
}
