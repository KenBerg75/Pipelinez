namespace Pipelinez.Kafka.Tests.Infrastructure;

public static class KafkaTopicNameFactory
{
    public static string CreateTopicName(string prefix, string scenarioName)
    {
        return CreateName(prefix, scenarioName);
    }

    public static string CreateConsumerGroupName(string prefix, string scenarioName)
    {
        return CreateName(prefix, $"{scenarioName}-group");
    }

    private static string CreateName(string prefix, string scenarioName)
    {
        var normalizedScenario = new string(
            scenarioName
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray())
            .Trim('-');

        return $"{prefix}-{normalizedScenario}-{Guid.NewGuid():N}";
    }
}
