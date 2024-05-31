using Pipelinez.Core;
using Pipelinez.Core.Segment;

namespace Pipelinez.Tests.Core.SegmentTests.Models;

public class TestOrderSecondSegment : PipelineSegment<TestOrderModel>
{
    public override Task<TestOrderModel> ExecuteAsync(TestOrderModel arg)
    {
        arg.SecondStamp = DateTime.UtcNow;
        return Task.FromResult(arg);
    }
}