using Confluent.Kafka;
using Examples.Shared;

var settings = KafkaExampleSettings.LoadFromEnvironment();
var messageCount = args.Length > 0 && int.TryParse(args[0], out var parsedCount) && parsedCount > 0
    ? parsedCount
    : settings.MessageCount;

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

await using var kafkaCluster = await KafkaExampleCluster.StartAsync(settings, cancellation.Token);
await kafkaCluster.EnsureTopicsAsync(cancellation.Token);

Console.WriteLine($"BootstrapServers: {kafkaCluster.BootstrapServers}");
Console.WriteLine($"SourceTopic: {kafkaCluster.SourceTopic}");
Console.WriteLine($"Publishing {messageCount} records...");

await kafkaCluster.ProduceAsync(
    Enumerable.Range(0, messageCount)
        .Select(index => new Message<string, string>
        {
            Key = $"datagen-key-{index}",
            Value = $"datagen-value-{index}",
            Headers = new Headers
            {
                { "example-source", System.Text.Encoding.UTF8.GetBytes("Example.Kafka.DataGen") }
            }
        }),
    cancellation.Token);

Console.WriteLine("Done.");
