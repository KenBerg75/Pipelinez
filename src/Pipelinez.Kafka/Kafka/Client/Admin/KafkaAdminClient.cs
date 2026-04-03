using Ardalis.GuardClauses;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Logging;
using Pipelinez.Kafka.Configuration;

namespace Pipelinez.Kafka.Client.Admin;

internal class KafkaAdminClient : IKafkaAdminClient
{
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaAdminClient> _logger;
    
    internal KafkaAdminClient(KafkaOptions config)
    {
        _options = config;
        _logger = LoggingManager.Instance.CreateLogger<KafkaAdminClient>();
    }
    
    /// <summary>
    /// Initiates a connection to Kafka to verify connectivity
    /// </summary>
    /// <returns>bool with True indicating a successful connection, and False a failed connection</returns>
    public bool CanConnect()
    {
        Guard.Against.Null(_options, nameof(_options), "Kafka options are required to create an Admin Client");
        try
        {
            using (var consumer = new AdminClientBuilder(_options.ToAdminClientConfig()).Build())
            {
                // Get some metadata to verify connectivity
                var metadata = consumer.GetMetadata(TimeSpan.FromSeconds(10));
                var topicsMetadata = metadata.Topics;
                var topicNames = metadata.Topics.Select(a => a.Topic).ToList();
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Admin client could not connect to kafka");
            return false;
        }
    }
    
}