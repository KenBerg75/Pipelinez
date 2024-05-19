using Pipelinez.Core.Flow;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Destination;


public interface IPipelineDestination<T> : IFlowDestination<PipelineContainer<T>> where T : PipelineRecord
{
    Task StartAsync(CancellationTokenSource cancellationToken);
    
    Task Completion { get; }
    
    void Initialize(Pipeline<T> parentPipeline);
}