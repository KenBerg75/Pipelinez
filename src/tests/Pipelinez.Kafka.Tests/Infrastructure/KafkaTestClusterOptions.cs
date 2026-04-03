namespace Pipelinez.Kafka.Tests.Infrastructure;

public sealed class KafkaTestClusterOptions
{
    public const string DefaultKafkaImage = "confluentinc/cp-kafka:7.6.1";
    public const string DefaultTopicPrefix = "pipelinez-kafka-tests";
    public static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan DefaultConsumeTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan DefaultObservationWindow = TimeSpan.FromSeconds(3);

    public string KafkaImage { get; init; } = DefaultKafkaImage;
    public TimeSpan StartupTimeout { get; init; } = DefaultStartupTimeout;
    public TimeSpan ConsumeTimeout { get; init; } = DefaultConsumeTimeout;
    public TimeSpan ObservationWindow { get; init; } = DefaultObservationWindow;
    public string TopicPrefix { get; init; } = DefaultTopicPrefix;
    public bool ReuseContainer { get; init; }
    public int AutoCommitIntervalMs { get; init; } = 250;

    public static KafkaTestClusterOptions LoadFromEnvironment()
    {
        return new KafkaTestClusterOptions
        {
            KafkaImage = GetEnvironmentValue("PIPELINEZ_KAFKA_TEST_IMAGE", DefaultKafkaImage),
            StartupTimeout = GetEnvironmentSeconds(
                "PIPELINEZ_KAFKA_TEST_STARTUP_TIMEOUT_SECONDS",
                DefaultStartupTimeout),
            ConsumeTimeout = GetEnvironmentSeconds(
                "PIPELINEZ_KAFKA_TEST_CONSUME_TIMEOUT_SECONDS",
                DefaultConsumeTimeout),
            ObservationWindow = GetEnvironmentSeconds(
                "PIPELINEZ_KAFKA_TEST_OBSERVATION_TIMEOUT_SECONDS",
                DefaultObservationWindow),
            TopicPrefix = GetEnvironmentValue("PIPELINEZ_KAFKA_TEST_TOPIC_PREFIX", DefaultTopicPrefix),
            ReuseContainer = GetEnvironmentBool("PIPELINEZ_KAFKA_TEST_REUSE_CONTAINER"),
            AutoCommitIntervalMs = GetEnvironmentInt("PIPELINEZ_KAFKA_TEST_AUTO_COMMIT_INTERVAL_MS", 250)
        };
    }

    private static string GetEnvironmentValue(string variableName, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
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
