using Pipelinez.Core.Destination;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.ErrorHandlingTests.Models;

public sealed class ConditionalFaultingDestination : PipelineDestination<TestPipelineRecord>
{
    public const string FaultingValue = "bad";
    public const string FailureMessage = "Destination failed intentionally from error-handler test.";

    protected override Task ExecuteAsync(TestPipelineRecord record, CancellationToken cancellationToken)
    {
        if (record.Data == FaultingValue)
        {
            throw new InvalidOperationException(FailureMessage);
        }

        return Task.CompletedTask;
    }

    protected override void Initialize()
    {
    }
}
