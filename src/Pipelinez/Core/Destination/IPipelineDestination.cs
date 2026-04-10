using Pipelinez.Core.Flow;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Destination;

/// <summary>
/// Defines a pipeline destination that consumes terminal pipeline containers.
/// </summary>
/// <typeparam name="T">The pipeline record type written by the destination.</typeparam>
public interface IPipelineDestination<T> : IFlowDestination<PipelineContainer<T>> where T : PipelineRecord
{
    /// <summary>
    /// Starts the destination execution loop.
    /// </summary>
    /// <param name="cancellationToken">The runtime cancellation source used to stop the destination.</param>
    Task StartAsync(CancellationTokenSource cancellationToken);

    /// <summary>
    /// Gets a task that completes when the destination has fully finished processing.
    /// </summary>
    Task Completion { get; }

    /// <summary>
    /// Initializes the destination with its parent pipeline.
    /// </summary>
    /// <param name="parentPipeline">The owning pipeline.</param>
    void Initialize(Pipeline<T> parentPipeline);
}
