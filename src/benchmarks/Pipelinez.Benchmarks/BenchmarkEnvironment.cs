namespace Pipelinez.Benchmarks;

internal static class BenchmarkEnvironment
{
    private const string DockerEnabledVariable = "PIPELINEZ_BENCH_ENABLE_DOCKER";
    private const string AzureServiceBusConnectionStringVariable = "PIPELINEZ_ASB_CONNECTION_STRING";

    public static bool DockerBenchmarksEnabled => GetBooleanEnvironmentValue(DockerEnabledVariable);

    public static string? AzureServiceBusConnectionString =>
        GetTrimmedEnvironmentValue(AzureServiceBusConnectionStringVariable);

    public static bool AzureServiceBusBenchmarksEnabled =>
        !string.IsNullOrWhiteSpace(AzureServiceBusConnectionString);

    public static Type[] GetRegisteredBenchmarkTypes()
    {
        var benchmarkTypes = new List<Type>
        {
            typeof(InMemoryPipelineBenchmarks)
        };

        if (DockerBenchmarksEnabled)
        {
            benchmarkTypes.AddRange(
            [
                typeof(KafkaSourceBenchmarks),
                typeof(KafkaDestinationBenchmarks),
                typeof(KafkaDeadLetterBenchmarks),
                typeof(RabbitMqSourceBenchmarks),
                typeof(RabbitMqDestinationBenchmarks),
                typeof(RabbitMqDeadLetterBenchmarks),
                typeof(AmazonS3SourceBenchmarks),
                typeof(AmazonS3DestinationBenchmarks),
                typeof(AmazonS3DeadLetterBenchmarks),
                typeof(PostgreSqlDestinationBenchmarks),
                typeof(PostgreSqlDeadLetterBenchmarks),
                typeof(SqlServerDestinationBenchmarks),
                typeof(SqlServerDeadLetterBenchmarks)
            ]);
        }

        if (AzureServiceBusBenchmarksEnabled)
        {
            benchmarkTypes.AddRange(
            [
                typeof(AzureServiceBusSourceBenchmarks),
                typeof(AzureServiceBusDestinationBenchmarks),
                typeof(AzureServiceBusDeadLetterBenchmarks)
            ]);
        }

        return benchmarkTypes.ToArray();
    }

    private static bool GetBooleanEnvironmentValue(string variableName)
    {
        var value = GetTrimmedEnvironmentValue(variableName);
        return bool.TryParse(value, out var parsed) && parsed;
    }

    private static string? GetTrimmedEnvironmentValue(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
