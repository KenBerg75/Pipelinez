using Pipelinez.Core.Destination;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.FaultHandlingTests.Models;

public sealed class FaultingDestination : PipelineDestination<TestPipelineRecord>
{
    public const string FailureMessage = "Destination failed intentionally.";

    protected override Task ExecuteAsync(TestPipelineRecord record, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    protected override void Initialize()
    {
    }
}
