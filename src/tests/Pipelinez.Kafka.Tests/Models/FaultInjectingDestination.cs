using System.Collections.Concurrent;
using Pipelinez.Core.Destination;

namespace Pipelinez.Kafka.Tests.Models;

public sealed class FaultInjectingDestination(string faultingValue) : PipelineDestination<TestKafkaRecord>
{
    public const string DefaultFailureMessage = "Kafka destination failed intentionally.";

    public ConcurrentQueue<string> ProcessedKeys { get; } = new();

    protected override Task ExecuteAsync(TestKafkaRecord record, CancellationToken cancellationToken)
    {
        if (record.Value.Equals(faultingValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(DefaultFailureMessage);
        }

        ProcessedKeys.Enqueue(record.Key);
        return Task.CompletedTask;
    }

    protected override void Initialize()
    {
    }
}
