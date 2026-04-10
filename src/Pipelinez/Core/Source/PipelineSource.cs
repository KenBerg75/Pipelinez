using System.Threading.Tasks.Dataflow;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Flow;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Operational;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Core.Source;

/// <summary>
/// Provides a base implementation for pipeline sources backed by a Dataflow buffer.
/// </summary>
/// <typeparam name="T">The pipeline record type produced by the source.</typeparam>
public abstract class PipelineSourceBase<T> : IPipelineSource<T>, IPipelineExecutionConfigurable, IPipelinePerformanceAware, IPipelineFlowStatusProvider where T : PipelineRecord
{
    private BufferBlock<PipelineContainer<T>>? _messageBuffer;
    private Pipeline<T>? _parentPipeline;
    private PipelineExecutionOptions _executionOptions = PipelineExecutionOptions.CreateDefaultSourceOptions();
    private IPipelinePerformanceCollector? _performanceCollector;
    private string _componentName = "Source";

    /// <summary>
    /// Gets the logger used by the source.
    /// </summary>
    protected ILogger<PipelineSourceBase<T>> Logger { get; }

    /// <summary>
    /// Initializes a new source base instance.
    /// </summary>
    public PipelineSourceBase()
    {
        Logger = LoggingManager.Instance.CreateLogger<PipelineSourceBase<T>>();
    }
    
    #region IPipelineSource
    
    /// <inheritdoc />
    public IDisposable ConnectTo(IFlowDestination<PipelineContainer<T>> target, DataflowLinkOptions? options = null)
    {
        options ??= new DataflowLinkOptions() { MaxMessages = DataflowBlockOptions.Unbounded };
        return MessageBuffer.LinkTo(target.AsTargetBlock(), options);
    }
    
    /// <inheritdoc />
    public async Task StartAsync(CancellationTokenSource cancellationToken)
    {
        try
        {
            Logger.LogInformation("Starting up pipeline source");
            await MainLoop(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error in the PipelineSource MainLoop()");
            Complete();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(T record)
    {
        var publishResult = await PublishAsync(record, new PipelinePublishOptions()).ConfigureAwait(false);
        HandleUnacceptedPublishResult(publishResult);
    }

    /// <inheritdoc />
    public Task<PipelinePublishResult> PublishAsync(T record, PipelinePublishOptions options)
    {
        return PublishAsync(record, new MetadataCollection(), options);
    }

    /// <inheritdoc />
    public async Task PublishAsync(T record, MetadataCollection metadata)
    {
        var publishResult = await PublishAsync(record, metadata, new PipelinePublishOptions()).ConfigureAwait(false);
        HandleUnacceptedPublishResult(publishResult);
    }

    /// <inheritdoc />
    public async Task<PipelinePublishResult> PublishAsync(T record, MetadataCollection metadata, PipelinePublishOptions options)
    {
        EnsureCorrelationId(metadata);

        var container = new PipelineContainer<T>(
            Guard.Against.Null(record, nameof(record)),
            Guard.Against.Null(metadata, nameof(metadata)));
        var validatedOptions = Guard.Against.Null(options, nameof(options)).Validate();
        var publishResult = await PipelineFlowController.PublishAsync(
                MessageBuffer,
                container,
                ParentPipeline.GetFlowControlOptions(),
                validatedOptions,
                ParentPipeline.GetRuntimeCancellationToken())
            .ConfigureAwait(false);

        if (publishResult.Accepted)
        {
            _performanceCollector?.RecordPublished(_componentName);
            ParentPipeline.NotifyPublishAccepted(publishResult.WaitDuration);
            return publishResult;
        }

        ParentPipeline.NotifyPublishRejected(record, metadata, publishResult);
        return publishResult;
    }

    /// <inheritdoc />
    public void Complete()
    {
        Logger.LogInformation("Completing pipeline source");
        MessageBuffer.Complete();
    }

    /// <inheritdoc />
    public Task Completion => MessageBuffer.Completion;


    /// <inheritdoc />
    public void Initialize(Pipeline<T> parentPipeline)
    {
        _parentPipeline = parentPipeline;
        _parentPipeline.OnPipelineContainerCompelted += OnPipelineContainerComplete;
        _parentPipeline.OnPipelineContainerFaultHandled += OnPipelineContainerFaultHandled;
        
        // Give a chance for inheritors to initialize
        Initialize();
    }

    /// <inheritdoc />
    public virtual void OnPipelineContainerComplete(object sender,
        PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e)
    {
        // Nothing to do yet
        // should use to support transactional processing
    }

    internal virtual void OnPipelineContainerFaultHandled(
        object sender,
        PipelineContainerFaultHandledEventHandlerArgs<PipelineContainer<T>> e)
    {
    }

    #endregion

    #region Performance

    /// <inheritdoc />
    public void ConfigureExecutionOptions(PipelineExecutionOptions options)
    {
        var validated = Guard.Against.Null(options, nameof(options)).Validate();
        EnsureExecutionOptionsCanBeChanged();
        _executionOptions = validated;
    }

    /// <inheritdoc />
    public PipelineExecutionOptions GetExecutionOptions()
    {
        return _executionOptions;
    }

    void IPipelinePerformanceAware.ConfigurePerformanceCollector(
        IPipelinePerformanceCollector performanceCollector,
        string componentName)
    {
        _performanceCollector = Guard.Against.Null(performanceCollector, nameof(performanceCollector));
        _componentName = Guard.Against.NullOrWhiteSpace(componentName, nameof(componentName));
    }

    #endregion
    
    #region Required Implementation
    
    /// <summary>
    /// Executes the main source loop.
    /// </summary>
    /// <param name="cancellationToken">The runtime cancellation source used to stop the source.</param>
    /// <returns>A task that completes when the source loop exits.</returns>
    protected abstract Task MainLoop(CancellationTokenSource cancellationToken);
    /// <summary>
    /// Provides an opportunity for the source to initialize transport-specific state.
    /// </summary>
    protected abstract void Initialize();
    
    #endregion

    /// <summary>
    /// Gets the parent pipeline.
    /// </summary>
    protected Pipeline<T> ParentPipeline =>
        _parentPipeline ?? throw new InvalidOperationException("Pipeline source has not been initialized.");

    private BufferBlock<PipelineContainer<T>> MessageBuffer =>
        _messageBuffer ??= new BufferBlock<PipelineContainer<T>>(new DataflowBlockOptions
        {
            BoundedCapacity = _executionOptions.BoundedCapacity,
            EnsureOrdered = _executionOptions.EnsureOrdered
        });

    int IPipelineFlowStatusProvider.GetApproximateQueueDepth()
    {
        return MessageBuffer.Count;
    }

    int? IPipelineFlowStatusProvider.GetBoundedCapacity()
    {
        return _executionOptions.BoundedCapacity == DataflowBlockOptions.Unbounded
            ? null
            : _executionOptions.BoundedCapacity;
    }

    private void EnsureExecutionOptionsCanBeChanged()
    {
        if (_messageBuffer is not null)
        {
            throw new InvalidOperationException(
                $"Execution options for source '{GetType().Name}' must be configured before the source is linked or used.");
        }
    }

    private void HandleUnacceptedPublishResult(PipelinePublishResult publishResult)
    {
        if (publishResult.Accepted)
        {
            return;
        }

        if (publishResult.Reason == PipelinePublishResultReason.Canceled &&
            ParentPipeline.GetRuntimeCancellationToken().IsCancellationRequested)
        {
            return;
        }

        publishResult.ThrowIfNotAccepted();
    }

    private static void EnsureCorrelationId(MetadataCollection metadata)
    {
        if (metadata.HasKey(PipelineOperationalMetadataKeys.CorrelationId))
        {
            return;
        }

        metadata.Set(PipelineOperationalMetadataKeys.CorrelationId, Guid.NewGuid().ToString("N"));
    }
}
