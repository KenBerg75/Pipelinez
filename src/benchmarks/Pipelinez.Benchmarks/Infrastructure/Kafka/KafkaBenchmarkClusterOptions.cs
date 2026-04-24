namespace Pipelinez.Benchmarks;

internal sealed class KafkaBenchmarkClusterOptions
{
    public const string DefaultKafkaImage = "confluentinc/cp-kafka:7.6.1";
    public const string DefaultTopicPrefix = "pipelinez-kafka-benchmarks";
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

    public static KafkaBenchmarkClusterOptions LoadFromEnvironment()
    {
        return new KafkaBenchmarkClusterOptions
        {
            KafkaImage = GetEnvironmentValue("PIPELINEZ_BENCH_KAFKA_IMAGE", "PIPELINEZ_KAFKA_TEST_IMAGE", DefaultKafkaImage),
            StartupTimeout = GetEnvironmentSeconds(
                "PIPELINEZ_BENCH_KAFKA_STARTUP_TIMEOUT_SECONDS",
                "PIPELINEZ_KAFKA_TEST_STARTUP_TIMEOUT_SECONDS",
                DefaultStartupTimeout),
            ConsumeTimeout = GetEnvironmentSeconds(
                "PIPELINEZ_BENCH_KAFKA_CONSUME_TIMEOUT_SECONDS",
                "PIPELINEZ_KAFKA_TEST_CONSUME_TIMEOUT_SECONDS",
                DefaultConsumeTimeout),
            ObservationWindow = GetEnvironmentSeconds(
                "PIPELINEZ_BENCH_KAFKA_OBSERVATION_TIMEOUT_SECONDS",
                "PIPELINEZ_KAFKA_TEST_OBSERVATION_TIMEOUT_SECONDS",
                DefaultObservationWindow),
            TopicPrefix = GetEnvironmentValue("PIPELINEZ_BENCH_KAFKA_TOPIC_PREFIX", "PIPELINEZ_KAFKA_TEST_TOPIC_PREFIX", DefaultTopicPrefix),
            ReuseContainer = GetEnvironmentBool("PIPELINEZ_BENCH_KAFKA_REUSE_CONTAINER", "PIPELINEZ_KAFKA_TEST_REUSE_CONTAINER"),
            AutoCommitIntervalMs = GetEnvironmentInt("PIPELINEZ_BENCH_KAFKA_AUTO_COMMIT_INTERVAL_MS", "PIPELINEZ_KAFKA_TEST_AUTO_COMMIT_INTERVAL_MS", 250)
        };
    }

    private static string GetEnvironmentValue(string preferredVariable, string fallbackVariable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(preferredVariable);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = Environment.GetEnvironmentVariable(fallbackVariable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static TimeSpan GetEnvironmentSeconds(string preferredVariable, string fallbackVariable, TimeSpan fallback)
    {
        var value = Environment.GetEnvironmentVariable(preferredVariable)
                    ?? Environment.GetEnvironmentVariable(fallbackVariable);

        return int.TryParse(value, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : fallback;
    }

    private static int GetEnvironmentInt(string preferredVariable, string fallbackVariable, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(preferredVariable)
                    ?? Environment.GetEnvironmentVariable(fallbackVariable);

        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }

    private static bool GetEnvironmentBool(string preferredVariable, string fallbackVariable)
    {
        var value = Environment.GetEnvironmentVariable(preferredVariable)
                    ?? Environment.GetEnvironmentVariable(fallbackVariable);

        return bool.TryParse(value, out var parsed) && parsed;
    }
}
