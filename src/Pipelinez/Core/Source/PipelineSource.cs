using System.Threading.Tasks.Dataflow;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Flow;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Core.Source;

public abstract class PipelineSourceBase<T> : IPipelineSource<T>, IPipelineExecutionConfigurable, IPipelinePerformanceAware where T : PipelineRecord
{
    private BufferBlock<PipelineContainer<T>>? _messageBuffer;
    private Pipeline<T>? _parentPipeline;
    private PipelineExecutionOptions _executionOptions = PipelineExecutionOptions.CreateDefaultSourceOptions();
    private IPipelinePerformanceCollector? _performanceCollector;
    private string _componentName = "Source";

    protected ILogger<PipelineSourceBase<T>> Logger { get; }

    public PipelineSourceBase()
    {
        Logger = LoggingManager.Instance.CreateLogger<PipelineSourceBase<T>>();
    }
    
    #region IPipelineSource
    
    /// <summary>
    /// Connect this source to the next segment in the pipeline
    /// </summary>
    /// <returns></returns>
    public IDisposable ConnectTo(IFlowDestination<PipelineContainer<T>> target, DataflowLinkOptions? options = null)
    {
        options ??= new DataflowLinkOptions() { MaxMessages = DataflowBlockOptions.Unbounded };
        return MessageBuffer.LinkTo(target.AsTargetBlock(), options);
    }
    
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

    public async Task PublishAsync(T record)
    {
        var container = new PipelineContainer<T>(record);

        await MessageBuffer.SendAsync(container).ConfigureAwait(false);
        _performanceCollector?.RecordPublished(_componentName);
    }

    public async Task PublishAsync(T record, MetadataCollection metadata)
    {
        var container = new PipelineContainer<T>(record, metadata);

        await MessageBuffer.SendAsync(container).ConfigureAwait(false);
        _performanceCollector?.RecordPublished(_componentName);
    }

    public void Complete()
    {
        Logger.LogInformation("Completing pipeline source");
        MessageBuffer.Complete();
    }

    public Task Completion => MessageBuffer.Completion;


    public void Initialize(Pipeline<T> parentPipeline)
    {
        _parentPipeline = parentPipeline;
        _parentPipeline.OnPipelineContainerCompelted += OnPipelineContainerComplete;
        
        // Give a chance for inheritors to initialize
        Initialize();
    }

    public virtual void OnPipelineContainerComplete(object sender,
        PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e)
    {
        // Nothing to do yet
        // should use to support transactional processing
    }

    #endregion

    #region Performance

    public void ConfigureExecutionOptions(PipelineExecutionOptions options)
    {
        var validated = Guard.Against.Null(options, nameof(options)).Validate();
        EnsureExecutionOptionsCanBeChanged();
        _executionOptions = validated;
    }

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
    /// This method should contain the logic for the pipeline source.
    /// Source method is async and should contain the complete application loop for the source
    /// </summary>
    /// <returns></returns>
    protected abstract Task MainLoop(CancellationTokenSource cancellationToken);
    /// <summary>
    /// Method to provide an opportunity for the source to initialize
    /// </summary>
    protected abstract void Initialize();
    
    #endregion

    protected Pipeline<T> ParentPipeline =>
        _parentPipeline ?? throw new InvalidOperationException("Pipeline source has not been initialized.");

    private BufferBlock<PipelineContainer<T>> MessageBuffer =>
        _messageBuffer ??= new BufferBlock<PipelineContainer<T>>(new DataflowBlockOptions
        {
            BoundedCapacity = _executionOptions.BoundedCapacity,
            EnsureOrdered = _executionOptions.EnsureOrdered
        });

    private void EnsureExecutionOptionsCanBeChanged()
    {
        if (_messageBuffer is not null)
        {
            throw new InvalidOperationException(
                $"Execution options for source '{GetType().Name}' must be configured before the source is linked or used.");
        }
    }
}
