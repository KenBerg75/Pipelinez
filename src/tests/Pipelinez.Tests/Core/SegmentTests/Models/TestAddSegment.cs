using Microsoft.Extensions.Logging;
using Pipelinez.Core;
using Pipelinez.Core.Segment;

namespace Pipelinez.Tests.Core.SegmentTests.Models;

public class TestAddSegment : PipelineSegment<TestSegmentModel>
{
    public override TestSegmentModel ExecuteAsync(TestSegmentModel arg)
    {
        Logger.LogInformation("I am adding some values");
        arg.AddResult = arg.FirstValue + arg.SecondValue;
        return arg;
    }
}