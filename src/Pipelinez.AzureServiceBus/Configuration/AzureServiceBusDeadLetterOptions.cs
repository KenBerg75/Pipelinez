namespace Pipelinez.AzureServiceBus.Configuration;

/// <summary>
/// Configures Azure Service Bus destination writes for Pipelinez dead-letter records.
/// </summary>
public sealed class AzureServiceBusDeadLetterOptions
{
    /// <summary>
    /// Gets or sets the Azure Service Bus connection options.
    /// </summary>
    public AzureServiceBusConnectionOptions Connection { get; set; } = new();

    /// <summary>
    /// Gets or sets the queue or topic entity to publish dead-letter records to.
    /// </summary>
    public AzureServiceBusEntityOptions Entity { get; set; } = new();

    /// <summary>
    /// Validates the dead-letter options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public AzureServiceBusDeadLetterOptions Validate()
    {
        ArgumentNullException.ThrowIfNull(Connection);
        ArgumentNullException.ThrowIfNull(Entity);

        Connection.Validate();
        Entity.ValidateForDestination();
        return this;
    }
}
