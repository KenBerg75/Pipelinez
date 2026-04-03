using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;

namespace Pipelinez.Kafka.Tests.Models;

public sealed class AddHeaderSegment(string key, string value) : PipelineSegment<TestKafkaRecord>
{
    public override Task<TestKafkaRecord> ExecuteAsync(TestKafkaRecord arg)
    {
        arg.Headers.Add(new PipelineRecordHeader
        {
            Key = key,
            Value = value
        });

        return Task.FromResult(arg);
    }
}
