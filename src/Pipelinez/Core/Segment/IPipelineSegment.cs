using Pipelinez.Core.Flow;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Segment;

public interface IPipelineSegment<T> : IFlowSource<PipelineContainer<T>>, IFlowDestination<PipelineContainer<T>> where T : PipelineRecord
{
    Task Completion { get; }
}