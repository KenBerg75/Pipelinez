using Pipelinez.Core.Segment;
using Pipelinez.Tests.Core.SegmentTests.Models;

namespace Pipelinez.Tests.Core.FaultHandlingTests.Models;

public sealed class FaultingSegment : PipelineSegment<TestSegmentModel>
{
    public const string FailureMessage = "Segment failed intentionally.";

    public override Task<TestSegmentModel> ExecuteAsync(TestSegmentModel arg)
    {
        throw new InvalidOperationException(FailureMessage);
    }
}
