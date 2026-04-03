using Confluent.Kafka;

namespace Pipelinez.Kafka.Tests.EndToEnd;

internal static class KafkaPipelineTestHelpers
{
    public static Message<string, string> CreateMessage(
        string key,
        string value,
        params (string Key, string Value)[] headers)
    {
        var message = new Message<string, string>
        {
            Key = key,
            Value = value,
            Headers = new Headers()
        };

        foreach (var (headerKey, headerValue) in headers)
        {
            message.Headers.Add(headerKey, System.Text.Encoding.UTF8.GetBytes(headerValue));
        }

        return message;
    }

    public static async Task WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for the expected integration test condition.");
    }
}
