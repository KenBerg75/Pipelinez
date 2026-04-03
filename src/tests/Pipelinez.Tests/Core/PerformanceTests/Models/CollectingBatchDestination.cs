using System.Collections.Concurrent;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Record;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.PerformanceTests.Models;

public sealed class CollectingBatchDestination : PipelineDestination<TestPipelineRecord>, IBatchedPipelineDestination<TestPipelineRecord>
{
    public ConcurrentQueue<int> BatchSizes { get; } = new();

    protected override Task ExecuteAsync(TestPipelineRecord record, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Single-record execution should not be used when batching is configured.");
    }

    public Task ExecuteBatchAsync(
        IReadOnlyList<PipelineContainer<TestPipelineRecord>> batch,
        CancellationToken cancellationToken)
    {
        BatchSizes.Enqueue(batch.Count);
        return Task.CompletedTask;
    }

    protected override void Initialize()
    {
    }
}
