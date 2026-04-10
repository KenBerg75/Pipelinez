using Pipelinez.Core.Record;

namespace Pipelinez.Core.Destination;

/// <summary>
/// Defines a destination that can process groups of pipeline containers in a single batch operation.
/// </summary>
/// <typeparam name="T">The pipeline record type written by the destination.</typeparam>
public interface IBatchedPipelineDestination<T> : IPipelineDestination<T>
    where T : PipelineRecord
{
    /// <summary>
    /// Executes a batch of records as a single destination operation.
    /// </summary>
    /// <param name="batch">The batch of pipeline containers to process.</param>
    /// <param name="cancellationToken">A token used to cancel the batch operation.</param>
    Task ExecuteBatchAsync(
        IReadOnlyList<PipelineContainer<T>> batch,
        CancellationToken cancellationToken);
}
