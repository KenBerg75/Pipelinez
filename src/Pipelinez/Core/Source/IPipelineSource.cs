using Pipelinez.Core.Eventing;
using Pipelinez.Core.Flow;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Core.Source;

public interface IPipelineSource<T> : IFlowSource<PipelineContainer<T>> where T : PipelineRecord
{
    Task StartAsync(CancellationTokenSource cancellationToken);
    
    Task PublishAsync(T record);

    Task PublishAsync(T record, MetadataCollection metadata);

    void Complete();
    Task Completion { get; }
    
    void Initialize(Pipeline<T> parentPipeline);
    
    /// <summary>
    /// Occurs when the record (internally the container) has completed traversing the pipeline
    /// </summary>
    void OnPipelineContainerComplete(object sender, PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e);
}