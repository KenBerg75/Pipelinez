namespace Pipelinez.Kafka.Client;

public interface IKafkaAdminClient
{
    /// <summary>
    /// Validates the connection to kafka
    /// </summary>
    bool CanConnect();
}