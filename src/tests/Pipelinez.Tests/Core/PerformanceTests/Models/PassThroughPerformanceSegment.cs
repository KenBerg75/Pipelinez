using Pipelinez.Core.Segment;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.PerformanceTests.Models;

public sealed class PassThroughPerformanceSegment : PipelineSegment<TestPipelineRecord>
{
    public override Task<TestPipelineRecord> ExecuteAsync(TestPipelineRecord arg)
    {
        return Task.FromResult(arg);
    }
}
