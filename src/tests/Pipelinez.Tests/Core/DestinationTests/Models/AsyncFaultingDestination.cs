using Pipelinez.Core.Destination;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.DestinationTests.Models;

public sealed class AsyncFaultingDestination : PipelineDestination<TestPipelineRecord>
{
    public const string FailureMessage = "Async destination failed intentionally.";

    protected override async Task ExecuteAsync(TestPipelineRecord record, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(FailureMessage);
    }

    protected override void Initialize()
    {
    }
}
