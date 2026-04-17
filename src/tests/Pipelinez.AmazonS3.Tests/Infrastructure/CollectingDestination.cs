using Pipelinez.Core.Destination;
using Pipelinez.Core.Record;

namespace Pipelinez.AmazonS3.Tests.Infrastructure;

internal sealed class CollectingDestination<T> : PipelineDestination<T>
    where T : PipelineRecord
{
    private readonly List<T> _records = new();

    public IReadOnlyList<T> Records => _records.ToArray();

    protected override Task ExecuteAsync(T record, CancellationToken cancellationToken)
    {
        _records.Add(record);
        return Task.CompletedTask;
    }

    protected override void Initialize()
    {
    }
}
