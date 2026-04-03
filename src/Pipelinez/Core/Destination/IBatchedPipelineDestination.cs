using Pipelinez.Core.Record;

namespace Pipelinez.Core.Destination;

public interface IBatchedPipelineDestination<T> : IPipelineDestination<T>
    where T : PipelineRecord
{
    Task ExecuteBatchAsync(
        IReadOnlyList<PipelineContainer<T>> batch,
        CancellationToken cancellationToken);
}
