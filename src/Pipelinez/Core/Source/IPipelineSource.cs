using Pipelinez.Core.Eventing;
using Pipelinez.Core.Flow;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Core.Source;

public interface IPipelineSource<T> : IFlowSource<PipelineContainer<T>> where T : PipelineRecord
{
    Task StartAsync(CancellationTokenSource cancellationToken);
    
    Task PublishAsync(T record);

    Task<PipelinePublishResult> PublishAsync(T record, PipelinePublishOptions options);

    Task PublishAsync(T record, MetadataCollection metadata);

    Task<PipelinePublishResult> PublishAsync(T record, MetadataCollection metadata, PipelinePublishOptions options);

    void Complete();
    Task Completion { get; }
    
    void Initialize(Pipeline<T> parentPipeline);
    
    /// <summary>
    /// Occurs when the record (internally the container) has completed traversing the pipeline
    /// </summary>
    void OnPipelineContainerComplete(object sender, PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e);
}
