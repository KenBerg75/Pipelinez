using Pipelinez.Core.Segment;

namespace Pipelinez.Kafka.Tests.Models;

public sealed class PartitionDelaySegment : PipelineSegment<TestKafkaRecord>
{
    public override async Task<TestKafkaRecord> ExecuteAsync(TestKafkaRecord arg)
    {
        if (arg.Value.Contains("slow", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(250).ConfigureAwait(false);
        }
        else
        {
            await Task.Delay(10).ConfigureAwait(false);
        }

        return arg;
    }
}
