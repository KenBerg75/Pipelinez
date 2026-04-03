using Confluent.Kafka;

namespace Pipelinez.Kafka.Configuration;

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string BootstrapUser { get; set; } = string.Empty;
    public string BootstrapPassword { get; set; } = string.Empty;
    public SecurityProtocol SecurityProtocol { get; set; } = SecurityProtocol.SaslSsl;
    public SaslMechanism SaslMechanism { get; set; } = SaslMechanism.Plain;

    internal bool UsesSaslAuthentication =>
        SecurityProtocol is SecurityProtocol.SaslPlaintext or SecurityProtocol.SaslSsl;
}
