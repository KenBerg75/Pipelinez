using RabbitMQ.Client;

namespace Pipelinez.RabbitMQ.Configuration;

/// <summary>
/// Configures how Pipelinez creates RabbitMQ connections.
/// </summary>
public sealed class RabbitMqConnectionOptions
{
    /// <summary>
    /// Gets or sets the AMQP URI used to connect.
    /// </summary>
    public Uri? Uri { get; set; }

    /// <summary>
    /// Gets or sets the RabbitMQ host name for host-based configuration.
    /// </summary>
    public string? HostName { get; set; }

    /// <summary>
    /// Gets or sets the RabbitMQ port.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Gets or sets the RabbitMQ virtual host.
    /// </summary>
    public string? VirtualHost { get; set; }

    /// <summary>
    /// Gets or sets the RabbitMQ user name.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the RabbitMQ password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the client-provided connection name.
    /// </summary>
    public string? ClientProvidedName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether automatic connection recovery is enabled.
    /// </summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether RabbitMQ.Client topology recovery is enabled.
    /// </summary>
    public bool TopologyRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the network recovery interval.
    /// </summary>
    public TimeSpan? NetworkRecoveryInterval { get; set; }

    /// <summary>
    /// Gets or sets the requested heartbeat interval.
    /// </summary>
    public TimeSpan? RequestedHeartbeat { get; set; }

    /// <summary>
    /// Gets or sets an optional callback that customizes the RabbitMQ connection factory after Pipelinez defaults are applied.
    /// </summary>
    public Action<ConnectionFactory>? ConfigureConnectionFactory { get; set; }

    /// <summary>
    /// Validates the connection options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public RabbitMqConnectionOptions Validate()
    {
        var hasUri = Uri is not null;
        var hasHostName = !string.IsNullOrWhiteSpace(HostName);

        if (hasUri && hasHostName)
        {
            throw new InvalidOperationException(
                $"{nameof(RabbitMqConnectionOptions)} cannot combine {nameof(Uri)} with {nameof(HostName)}.");
        }

        if (!hasUri && !hasHostName)
        {
            throw new InvalidOperationException(
                $"{nameof(RabbitMqConnectionOptions)} requires either {nameof(Uri)} or {nameof(HostName)}.");
        }

        if (Port.HasValue && Port.Value <= 0)
        {
            throw new InvalidOperationException($"{nameof(Port)} must be greater than zero when specified.");
        }

        return this;
    }

    internal ConnectionFactory CreateConnectionFactory(string defaultClientProvidedName)
    {
        Validate();

        var factory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = AutomaticRecoveryEnabled,
            TopologyRecoveryEnabled = TopologyRecoveryEnabled,
            ClientProvidedName = string.IsNullOrWhiteSpace(ClientProvidedName)
                ? defaultClientProvidedName
                : ClientProvidedName
        };

        if (Uri is not null)
        {
            factory.Uri = Uri;
        }
        else
        {
            factory.HostName = HostName!;
        }

        if (Port.HasValue)
        {
            factory.Port = Port.Value;
        }

        if (!string.IsNullOrWhiteSpace(VirtualHost))
        {
            factory.VirtualHost = VirtualHost;
        }

        if (!string.IsNullOrWhiteSpace(UserName))
        {
            factory.UserName = UserName;
        }

        if (!string.IsNullOrWhiteSpace(Password))
        {
            factory.Password = Password;
        }

        if (NetworkRecoveryInterval.HasValue)
        {
            factory.NetworkRecoveryInterval = NetworkRecoveryInterval.Value;
        }

        if (RequestedHeartbeat.HasValue)
        {
            factory.RequestedHeartbeat = RequestedHeartbeat.Value;
        }

        ConfigureConnectionFactory?.Invoke(factory);
        return factory;
    }
}
