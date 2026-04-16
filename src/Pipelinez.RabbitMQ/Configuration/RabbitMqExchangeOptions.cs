namespace Pipelinez.RabbitMQ.Configuration;

/// <summary>
/// Configures a RabbitMQ exchange declaration.
/// </summary>
public sealed class RabbitMqExchangeOptions
{
    /// <summary>
    /// Gets or sets the exchange name. Use an empty name for the default exchange.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exchange type.
    /// </summary>
    public RabbitMqExchangeType Type { get; set; } = RabbitMqExchangeType.Direct;

    /// <summary>
    /// Gets or sets a value indicating whether the exchange should be durable.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the exchange should auto-delete.
    /// </summary>
    public bool AutoDelete { get; set; }

    /// <summary>
    /// Gets or sets declaration arguments.
    /// </summary>
    public IDictionary<string, object?>? Arguments { get; set; }

    /// <summary>
    /// Creates exchange options for the specified exchange.
    /// </summary>
    /// <param name="name">The exchange name.</param>
    /// <param name="type">The exchange type.</param>
    /// <returns>The exchange options.</returns>
    public static RabbitMqExchangeOptions Named(
        string name,
        RabbitMqExchangeType type = RabbitMqExchangeType.Direct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new RabbitMqExchangeOptions
        {
            Name = name,
            Type = type
        };
    }

    /// <summary>
    /// Validates the exchange options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public RabbitMqExchangeOptions Validate()
    {
        if (!Enum.IsDefined(Type))
        {
            throw new InvalidOperationException($"Unsupported RabbitMQ exchange type '{Type}'.");
        }

        return this;
    }

    internal string GetExchangeTypeName()
    {
        return Type switch
        {
            RabbitMqExchangeType.Direct => "direct",
            RabbitMqExchangeType.Fanout => "fanout",
            RabbitMqExchangeType.Topic => "topic",
            RabbitMqExchangeType.Headers => "headers",
            _ => throw new InvalidOperationException($"Unsupported RabbitMQ exchange type '{Type}'.")
        };
    }
}
