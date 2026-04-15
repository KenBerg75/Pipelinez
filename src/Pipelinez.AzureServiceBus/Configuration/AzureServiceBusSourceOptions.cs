using Azure.Messaging.ServiceBus;

namespace Pipelinez.AzureServiceBus.Configuration;

/// <summary>
/// Configures an Azure Service Bus queue or topic subscription source.
/// </summary>
public sealed class AzureServiceBusSourceOptions
{
    /// <summary>
    /// Gets or sets the Azure Service Bus connection options.
    /// </summary>
    public AzureServiceBusConnectionOptions Connection { get; set; } = new();

    /// <summary>
    /// Gets or sets the queue or topic subscription entity to consume.
    /// </summary>
    public AzureServiceBusEntityOptions Entity { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum number of concurrent message callbacks.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of messages that the processor can eagerly request.
    /// </summary>
    public int PrefetchCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration for automatic message-lock renewal.
    /// </summary>
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets source settlement behavior.
    /// </summary>
    public AzureServiceBusSourceSettlementOptions Settlement { get; set; } = new();

    /// <summary>
    /// Gets or sets an optional callback that customizes processor options after Pipelinez defaults are applied.
    /// </summary>
    public Action<ServiceBusProcessorOptions>? ConfigureProcessor { get; set; }

    /// <summary>
    /// Validates the source options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public AzureServiceBusSourceOptions Validate()
    {
        ArgumentNullException.ThrowIfNull(Connection);
        ArgumentNullException.ThrowIfNull(Entity);
        ArgumentNullException.ThrowIfNull(Settlement);

        Connection.Validate();
        Entity.ValidateForSource();
        Settlement.Validate();

        if (MaxConcurrentCalls <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxConcurrentCalls)} must be greater than zero.");
        }

        if (PrefetchCount < 0)
        {
            throw new InvalidOperationException($"{nameof(PrefetchCount)} cannot be negative.");
        }

        if (MaxAutoLockRenewalDuration < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(MaxAutoLockRenewalDuration)} cannot be negative.");
        }

        return this;
    }
}
