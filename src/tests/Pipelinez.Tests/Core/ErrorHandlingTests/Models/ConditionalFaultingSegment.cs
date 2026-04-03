using Pipelinez.Core.Segment;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.ErrorHandlingTests.Models;

public sealed class ConditionalFaultingSegment : PipelineSegment<TestPipelineRecord>
{
    public const string FaultingValue = "bad";
    public const string FailureMessage = "Segment failed intentionally from error-handler test.";

    public override Task<TestPipelineRecord> ExecuteAsync(TestPipelineRecord arg)
    {
        if (arg.Data == FaultingValue)
        {
            throw new InvalidOperationException(FailureMessage);
        }

        arg.Data = $"{arg.Data}|processed";
        return Task.FromResult(arg);
    }
}
