using Pipelinez.Core;
using Pipelinez.Core.Segment;

namespace Pipelinez.Tests.Core.SourceDestTests.Models;

public class TestSegment : PipelineSegment<TestPipelineRecord>
{
    public override Task<TestPipelineRecord> ExecuteAsync(TestPipelineRecord arg)
    {
        return Task.FromResult(arg);
    }
}