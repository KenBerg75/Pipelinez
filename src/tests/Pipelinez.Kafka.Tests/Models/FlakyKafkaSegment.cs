using System.Collections.Concurrent;
using Pipelinez.Core.Segment;

namespace Pipelinez.Kafka.Tests.Models;

public sealed class FlakyKafkaSegment : PipelineSegment<TestKafkaRecord>
{
    private readonly ConcurrentDictionary<string, int> _attempts = new(StringComparer.Ordinal);

    public override Task<TestKafkaRecord> ExecuteAsync(TestKafkaRecord arg)
    {
        var attempts = _attempts.AddOrUpdate(arg.Key, 1, (_, current) => current + 1);

        if (attempts == 1)
        {
            throw new InvalidOperationException("Kafka segment failed transiently.");
        }

        arg.Value = $"{arg.Value}|retried";
        return Task.FromResult(arg);
    }
}
