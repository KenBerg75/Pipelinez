using Confluent.Kafka;

namespace Pipelinez.Kafka.Configuration;

internal static class KafkaConfigurationExtensions
{
    /// <summary>
    /// Converts a KafkaOptions object to an AdminClientConfig object
    /// </summary>
    internal static AdminClientConfig ToAdminClientConfig(this KafkaOptions options)
    {
        var config = new AdminClientConfig()
        {
            BootstrapServers = options.BootstrapServers,
            SecurityProtocol = options.SecurityProtocol
        };

        ApplySaslAuthentication(config, options);
        return config;
    }
    
    internal static ConsumerConfig ToConsumerConfig(this KafkaSourceOptions options, string pipelineName)
    {
        var config = new ConsumerConfig()
        {
            BootstrapServers = options.BootstrapServers,
            SecurityProtocol = options.SecurityProtocol,
            GroupId = options.ConsumerGroup,
            ClientId = $"{pipelineName}-con",
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        if (options.AutoCommitIntervalMs.HasValue)
        {
            config.AutoCommitIntervalMs = options.AutoCommitIntervalMs.Value;
        }

        ApplySaslAuthentication(config, options);
        return config;
    }

    internal static ProducerConfig ToProducerConfig(this KafkaDestinationOptions options, string pipelineName)
    {
        var config = new ProducerConfig()
        {
            BootstrapServers = options.BootstrapServers,
            SecurityProtocol = options.SecurityProtocol,
            ClientId = $"{pipelineName}-pub",
            Partitioner = Partitioner.Murmur2Random
        };

        ApplySaslAuthentication(config, options);
        return config;
    }

    private static void ApplySaslAuthentication(ClientConfig config, KafkaOptions options)
    {
        if (!options.UsesSaslAuthentication)
        {
            return;
        }

        config.SaslUsername = options.BootstrapUser;
        config.SaslPassword = options.BootstrapPassword;
        config.SaslMechanism = options.SaslMechanism;
    }
}
