using Pipelinez.Core.Destination;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.RetryTests.Models;

public sealed class FlakyDestination : PipelineDestination<TestPipelineRecord>
{
    private int _remainingFailures;

    public FlakyDestination(int failuresBeforeSuccess)
    {
        _remainingFailures = failuresBeforeSuccess;
    }

    public int Attempts { get; private set; }

    public List<string> ReceivedRecords { get; } = new();

    protected override Task ExecuteAsync(TestPipelineRecord record, CancellationToken cancellationToken)
    {
        Attempts++;

        if (_remainingFailures-- > 0)
        {
            throw new RetryTestException("Destination failed transiently.");
        }

        ReceivedRecords.Add(record.Data);
        return Task.CompletedTask;
    }

    protected override void Initialize()
    {
    }
}
