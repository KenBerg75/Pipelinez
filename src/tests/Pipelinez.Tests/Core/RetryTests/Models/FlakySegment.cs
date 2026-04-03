using Pipelinez.Core.Segment;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.RetryTests.Models;

public sealed class FlakySegment : PipelineSegment<TestPipelineRecord>
{
    private int _remainingFailures;
    private readonly bool _throwNonRetryable;

    public FlakySegment(int failuresBeforeSuccess, bool throwNonRetryable = false)
    {
        _remainingFailures = failuresBeforeSuccess;
        _throwNonRetryable = throwNonRetryable;
    }

    public int Attempts { get; private set; }

    public override Task<TestPipelineRecord> ExecuteAsync(TestPipelineRecord arg)
    {
        Attempts++;

        if (_remainingFailures-- > 0)
        {
            if (_throwNonRetryable)
            {
                throw new NonRetryableTestException("Segment threw a non-retryable exception.");
            }

            throw new RetryTestException("Segment failed transiently.");
        }

        arg.Data = $"{arg.Data}|segment-ok";
        return Task.FromResult(arg);
    }
}
