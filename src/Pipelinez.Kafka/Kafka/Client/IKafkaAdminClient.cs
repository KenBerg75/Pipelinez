namespace Pipelinez.Kafka.Client;

internal interface IKafkaAdminClient
{
    /// <summary>
    /// Validates the connection to kafka
    /// </summary>
    bool CanConnect();
}
