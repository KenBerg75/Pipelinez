using Pipelinez.Core.Segment;

namespace Pipelinez.Kafka.Tests.Models;

public sealed class ConditionalFaultingKafkaSegment(string faultingValue) : PipelineSegment<TestKafkaRecord>
{
    public const string DefaultFailureMessage = "Kafka segment failed intentionally.";

    public override Task<TestKafkaRecord> ExecuteAsync(TestKafkaRecord arg)
    {
        if (arg.Value.Equals(faultingValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(DefaultFailureMessage);
        }

        return Task.FromResult(arg);
    }
}
