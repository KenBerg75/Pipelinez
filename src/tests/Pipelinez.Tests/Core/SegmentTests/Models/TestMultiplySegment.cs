using Pipelinez.Core;
using Pipelinez.Core.Segment;

namespace Pipelinez.Tests.Core.SegmentTests.Models;

public class TestMultiplySegment : PipelineSegment<TestSegmentModel>
{
    public override Task<TestSegmentModel> ExecuteAsync(TestSegmentModel arg)
    {
        arg.MultiplyResult = arg.FirstValue * arg.SecondValue;
        return Task.FromResult(arg);
    }
}