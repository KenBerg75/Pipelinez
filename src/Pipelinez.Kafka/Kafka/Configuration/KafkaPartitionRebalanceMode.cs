namespace Pipelinez.Kafka.Configuration;

public enum KafkaPartitionRebalanceMode
{
    DrainAndYield,
    StopAcceptingNewWorkAndYield
}
