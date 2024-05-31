using Confluent.Kafka;

namespace Pipelinez.Kafka.Configuration;

internal static class KafkaConfigurationExtensions
{
    /// <summary>
    /// Converts a KafkaOptions object to an AdminClientConfig object
    /// </summary>
    internal static AdminClientConfig ToAdminClientConfig(this KafkaOptions options)
    {
        return new AdminClientConfig()
        {
            BootstrapServers = options.BootstrapServers,
            SaslUsername = options.BootstrapUser,
            SaslPassword = options.BootstrapPassword,
            SaslMechanism = SaslMechanism.Plain,
            SecurityProtocol = SecurityProtocol.SaslSsl
        };
       
    }
    
    internal static ConsumerConfig ToConsumerConfig(this KafkaSourceOptions options, string pipelineName)
    {
        return new ConsumerConfig()
        {
            BootstrapServers = options.BootstrapServers,
            SaslUsername = options.BootstrapUser,
            SaslPassword = options.BootstrapPassword,
            SaslMechanism = SaslMechanism.Plain,
            SecurityProtocol = SecurityProtocol.SaslSsl,
            GroupId = options.ConsumerGroup,
            ClientId = $"{pipelineName}-con",
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false,
            //AutoCommitIntervalMs = 30_000,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
    }
}