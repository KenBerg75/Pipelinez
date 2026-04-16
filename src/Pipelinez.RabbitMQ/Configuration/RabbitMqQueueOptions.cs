namespace Pipelinez.RabbitMQ.Configuration;

/// <summary>
/// Configures a RabbitMQ queue.
/// </summary>
public sealed class RabbitMqQueueOptions
{
    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the queue should be durable.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the queue should be exclusive.
    /// </summary>
    public bool Exclusive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue should auto-delete.
    /// </summary>
    public bool AutoDelete { get; set; }

    /// <summary>
    /// Gets or sets declaration arguments.
    /// </summary>
    public IDictionary<string, object?>? Arguments { get; set; }

    /// <summary>
    /// Creates queue options for the specified queue.
    /// </summary>
    /// <param name="name">The queue name.</param>
    /// <returns>The queue options.</returns>
    public static RabbitMqQueueOptions Named(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new RabbitMqQueueOptions
        {
            Name = name
        };
    }

    /// <summary>
    /// Validates the queue options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public RabbitMqQueueOptions Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException($"{nameof(Name)} is required for RabbitMQ queues.");
        }

        return this;
    }
}
