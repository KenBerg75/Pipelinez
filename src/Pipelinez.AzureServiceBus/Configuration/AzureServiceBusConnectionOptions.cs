using Azure.Core;
using Azure.Messaging.ServiceBus;

namespace Pipelinez.AzureServiceBus.Configuration;

/// <summary>
/// Configures how Pipelinez creates Azure Service Bus clients.
/// </summary>
public sealed class AzureServiceBusConnectionOptions
{
    /// <summary>
    /// Gets or sets the Azure Service Bus connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified namespace used with token-based authentication.
    /// </summary>
    /// <example>contoso.servicebus.windows.net</example>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>
    /// Gets or sets the token credential used with <see cref="FullyQualifiedNamespace" />.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets optional Azure Service Bus client options.
    /// </summary>
    public ServiceBusClientOptions? ClientOptions { get; set; }

    /// <summary>
    /// Validates the connection options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public AzureServiceBusConnectionOptions Validate()
    {
        var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);
        var hasNamespace = !string.IsNullOrWhiteSpace(FullyQualifiedNamespace);

        if (hasConnectionString && hasNamespace)
        {
            throw new InvalidOperationException(
                $"{nameof(AzureServiceBusConnectionOptions)} cannot combine {nameof(ConnectionString)} with {nameof(FullyQualifiedNamespace)}.");
        }

        if (hasConnectionString)
        {
            return this;
        }

        if (!hasNamespace)
        {
            throw new InvalidOperationException(
                $"{nameof(AzureServiceBusConnectionOptions)} requires either {nameof(ConnectionString)} or {nameof(FullyQualifiedNamespace)}.");
        }

        if (Credential is null)
        {
            throw new InvalidOperationException(
                $"{nameof(AzureServiceBusConnectionOptions)} requires {nameof(Credential)} when {nameof(FullyQualifiedNamespace)} is used.");
        }

        return this;
    }
}
