namespace Pipelinez.RabbitMQ.Tests.Infrastructure;

internal static class RabbitMqNameFactory
{
    public static string CreateName(string scenarioName)
    {
        var sanitized = new string(scenarioName
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray());
        return $"pipelinez-{sanitized}-{Guid.NewGuid():N}";
    }
}
