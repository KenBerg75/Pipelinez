namespace Pipelinez.Kafka.Configuration;

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string BootstrapUser { get; set; } = string.Empty;
    public string BootstrapPassword { get; set; } = string.Empty;
}