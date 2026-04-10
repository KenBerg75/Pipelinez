using Confluent.Kafka;

namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// Provides shared Kafka broker connection settings used by Kafka sources and destinations.
/// </summary>
public class KafkaOptions
{
    /// <summary>
    /// Gets or sets the bootstrap broker list used to connect to the Kafka cluster.
    /// </summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SASL username used when the selected security protocol requires authentication.
    /// </summary>
    public string BootstrapUser { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SASL password used when the selected security protocol requires authentication.
    /// </summary>
    public string BootstrapPassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transport security protocol used for the Kafka connection.
    /// </summary>
    public SecurityProtocol SecurityProtocol { get; set; } = SecurityProtocol.SaslSsl;

    /// <summary>
    /// Gets or sets the SASL mechanism used when SASL authentication is enabled.
    /// </summary>
    public SaslMechanism SaslMechanism { get; set; } = SaslMechanism.Plain;

    internal bool UsesSaslAuthentication =>
        SecurityProtocol is SecurityProtocol.SaslPlaintext or SecurityProtocol.SaslSsl;
}
