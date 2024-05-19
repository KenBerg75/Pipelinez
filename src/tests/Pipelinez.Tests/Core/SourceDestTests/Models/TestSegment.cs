using Pipelinez.Core;
using Pipelinez.Core.Segment;

namespace Pipelinez.Tests.Core.SourceDestTests.Models;

public class TestSegment : PipelineSegment<TestPipelineRecord>
{
    public override TestPipelineRecord ExecuteAsync(TestPipelineRecord arg)
    {
        return arg;
    }
}