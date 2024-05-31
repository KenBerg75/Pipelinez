using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Flow;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Core.Source;

public abstract class PipelineSourceBase<T> : IPipelineSource<T> where T : PipelineRecord
{
    private readonly BufferBlock<PipelineContainer<T>> _messageBuffer;
    private Pipeline<T> _parentPipeline;

    protected ILogger<PipelineSourceBase<T>> Logger { get; }

    public PipelineSourceBase()
    {
        _messageBuffer = new BufferBlock<PipelineContainer<T>>();
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
        return _messageBuffer.LinkTo(target.AsTargetBlock(), options);
    }
    
    public async Task StartAsync(CancellationTokenSource cancellationToken)
    {
        try
        {
            Logger.LogInformation("Starting up pipeline source");
            await MainLoop(cancellationToken);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error in the PipelineSource MainLoop()");
            Complete();
        }
    }

    public async Task PublishAsync(T record)
    {
        // ToDo: Needs to be refactored - maybe move to a factory
        var container = new PipelineContainer<T>(record);
        
        await _messageBuffer.SendAsync(container);
        
        #region Old Implementation - left in case SendAsync doesn't support buffer full
        /*
        // Processing loop - supporting the buffer being full. 
        const int retryBackoff = 2000;
        var wasBlocked = false;
        
        
        while (!_messageBuffer.Post(container)) 
        {
            //Log.Logger.Warning("Pipeline source message buffer full, blocking until available");
            wasBlocked = true;
            Thread.Sleep(retryBackoff);
        }

        if (wasBlocked)
        {
            //Log.Logger.Information("Pipeline source message buffer accepting records again");
            wasBlocked = false;
        }*/
        #endregion
    }

    public async Task PublishAsync(T record, MetadataCollection metadata)
    {
        // ToDo: Needs to be refactored - maybe move to a factory
        var container = new PipelineContainer<T>(record, metadata);
        
        await _messageBuffer.SendAsync(container);
    }

    public void Complete()
    {
        Logger.LogInformation("Completing pipeline source");
        _messageBuffer.Complete();
    }

    public Task Completion => _messageBuffer.Completion;


    public void Initialize(Pipeline<T> parentPipeline)
    {
        this._parentPipeline = parentPipeline;
        this._parentPipeline.OnPipelineContainerCompelted += OnPipelineContainerComplete;
        
        // Give a chance for inheritors to initialize
        this.Initialize();
    }

    public virtual void OnPipelineContainerComplete(object sender,
        PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e)
    {
        // Nothing to do yet
        // should use to support transactional processing
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
}