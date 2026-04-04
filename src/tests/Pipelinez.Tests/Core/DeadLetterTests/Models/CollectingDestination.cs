using System.Collections.Concurrent;
using Pipelinez.Core.Destination;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.DeadLetterTests.Models;

public sealed class CollectingDestination : PipelineDestination<TestPipelineRecord>
{
    public ConcurrentQueue<TestPipelineRecord> Records { get; } = new();

    protected override Task ExecuteAsync(TestPipelineRecord record, CancellationToken cancellationToken)
    {
        Records.Enqueue(record);
        return Task.CompletedTask;
    }

    protected override void Initialize()
    {
    }
}
