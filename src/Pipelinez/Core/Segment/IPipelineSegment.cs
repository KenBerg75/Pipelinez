using Pipelinez.Core.Flow;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Segment;

/// <summary>
/// Defines a pipeline segment that transforms records between the source and destination.
/// </summary>
/// <typeparam name="T">The pipeline record type processed by the segment.</typeparam>
public interface IPipelineSegment<T> : IFlowSource<PipelineContainer<T>>, IFlowDestination<PipelineContainer<T>> where T : PipelineRecord
{
    /// <summary>
    /// Gets a task that completes when the segment has finished processing.
    /// </summary>
    Task Completion { get; }
}
