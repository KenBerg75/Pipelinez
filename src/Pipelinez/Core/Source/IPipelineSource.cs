using Pipelinez.Core.Eventing;
using Pipelinez.Core.Flow;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Core.Source;

/// <summary>
/// Defines a pipeline source that can publish records into the pipeline runtime.
/// </summary>
/// <typeparam name="T">The pipeline record type produced by the source.</typeparam>
public interface IPipelineSource<T> : IFlowSource<PipelineContainer<T>> where T : PipelineRecord
{
    /// <summary>
    /// Starts the source execution loop.
    /// </summary>
    /// <param name="cancellationToken">The runtime cancellation source used to stop the source.</param>
    Task StartAsync(CancellationTokenSource cancellationToken);

    /// <summary>
    /// Publishes a record using the pipeline's default flow-control behavior.
    /// </summary>
    /// <param name="record">The record to publish.</param>
    Task PublishAsync(T record);

    /// <summary>
    /// Publishes a record using explicit publish options.
    /// </summary>
    /// <param name="record">The record to publish.</param>
    /// <param name="options">The publish options to apply.</param>
    /// <returns>The publish result.</returns>
    Task<PipelinePublishResult> PublishAsync(T record, PipelinePublishOptions options);

    /// <summary>
    /// Publishes a record with explicit metadata using the pipeline's default flow-control behavior.
    /// </summary>
    /// <param name="record">The record to publish.</param>
    /// <param name="metadata">The metadata to attach to the record.</param>
    Task PublishAsync(T record, MetadataCollection metadata);

    /// <summary>
    /// Publishes a record with explicit metadata and publish options.
    /// </summary>
    /// <param name="record">The record to publish.</param>
    /// <param name="metadata">The metadata to attach to the record.</param>
    /// <param name="options">The publish options to apply.</param>
    /// <returns>The publish result.</returns>
    Task<PipelinePublishResult> PublishAsync(T record, MetadataCollection metadata, PipelinePublishOptions options);

    /// <summary>
    /// Marks the source as complete so no additional records will be published.
    /// </summary>
    void Complete();

    /// <summary>
    /// Gets a task that completes when the source has finished publishing records.
    /// </summary>
    Task Completion { get; }

    /// <summary>
    /// Initializes the source with its parent pipeline.
    /// </summary>
    /// <param name="parentPipeline">The owning pipeline.</param>
    void Initialize(Pipeline<T> parentPipeline);

    /// <summary>
    /// Handles notification that a published container completed successfully.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The completed container event arguments.</param>
    void OnPipelineContainerComplete(object sender, PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e);
}
