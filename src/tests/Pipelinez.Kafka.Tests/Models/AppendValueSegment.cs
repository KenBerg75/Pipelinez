using Pipelinez.Core.Segment;

namespace Pipelinez.Kafka.Tests.Models;

public sealed class AppendValueSegment(string suffix) : PipelineSegment<TestKafkaRecord>
{
    public override Task<TestKafkaRecord> ExecuteAsync(TestKafkaRecord arg)
    {
        arg.Value = $"{arg.Value}{suffix}";
        return Task.FromResult(arg);
    }
}
