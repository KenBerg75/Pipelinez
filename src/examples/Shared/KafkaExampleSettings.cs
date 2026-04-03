namespace Examples.Shared;

public sealed class KafkaExampleSettings
{
    public const string DefaultKafkaImage = "confluentinc/cp-kafka:7.6.1";
    public const string DefaultSourceTopic = "pipelinez-example-source";
    public const string DefaultDestinationTopic = "pipelinez-example-destination";
    public static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromMinutes(2);

    public string? BootstrapServersOverride { get; init; }
    public string KafkaImage { get; init; } = DefaultKafkaImage;
    public TimeSpan StartupTimeout { get; init; } = DefaultStartupTimeout;
    public bool ReuseContainer { get; init; }
    public string SourceTopic { get; init; } = DefaultSourceTopic;
    public string DestinationTopic { get; init; } = DefaultDestinationTopic;
    public string ConsumerGroup { get; init; } = $"pipelinez-example-{Guid.NewGuid():N}";
    public int MessageCount { get; init; } = 5;

    public bool ShouldStartContainer => string.IsNullOrWhiteSpace(BootstrapServersOverride);

    public static KafkaExampleSettings LoadFromEnvironment()
    {
        return new KafkaExampleSettings
        {
            BootstrapServersOverride = GetOptionalEnvironmentValue("PIPELINEZ_EXAMPLE_BOOTSTRAP_SERVERS"),
            KafkaImage = GetEnvironmentValue("PIPELINEZ_EXAMPLE_KAFKA_IMAGE", DefaultKafkaImage),
            StartupTimeout = GetEnvironmentSeconds(
                "PIPELINEZ_EXAMPLE_KAFKA_STARTUP_TIMEOUT_SECONDS",
                DefaultStartupTimeout),
            ReuseContainer = GetEnvironmentBool("PIPELINEZ_EXAMPLE_KAFKA_REUSE_CONTAINER"),
            SourceTopic = GetEnvironmentValue("PIPELINEZ_EXAMPLE_SOURCE_TOPIC", DefaultSourceTopic),
            DestinationTopic = GetEnvironmentValue("PIPELINEZ_EXAMPLE_DESTINATION_TOPIC", DefaultDestinationTopic),
            ConsumerGroup = GetEnvironmentValue(
                "PIPELINEZ_EXAMPLE_CONSUMER_GROUP",
                $"pipelinez-example-{Guid.NewGuid():N}"),
            MessageCount = GetEnvironmentInt("PIPELINEZ_EXAMPLE_MESSAGE_COUNT", 5)
        };
    }

    private static string GetEnvironmentValue(string variableName, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string? GetOptionalEnvironmentValue(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static TimeSpan GetEnvironmentSeconds(string variableName, TimeSpan fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return int.TryParse(value, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : fallback;
    }

    private static int GetEnvironmentInt(string variableName, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }

    private static bool GetEnvironmentBool(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return bool.TryParse(value, out var parsed) && parsed;
    }
}
