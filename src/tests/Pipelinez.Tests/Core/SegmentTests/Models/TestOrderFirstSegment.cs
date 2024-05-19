using Pipelinez.Core;
using Pipelinez.Core.Segment;

namespace Pipelinez.Tests.Core.SegmentTests.Models;

public class TestOrderFirstSegment : PipelineSegment<TestOrderModel>
{
    public override TestOrderModel ExecuteAsync(TestOrderModel arg)
    {
        arg.FirstStamp = DateTime.UtcNow;
        return arg;
    }
}