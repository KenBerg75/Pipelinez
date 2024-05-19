using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Destination;


public abstract class PipelineDestination<T> : IPipelineDestination<T> where T : PipelineRecord
{
    private readonly BufferBlock<PipelineContainer<T>> _messageBuffer;
    private Pipeline<T> _parentPipeline;
    protected ILogger<PipelineDestination<T>> Logger { get; }

    #region Constructor
    
    public PipelineDestination()
    {
        Logger = LoggingManager.Instance.CreateLogger<PipelineDestination<T>>();
        _messageBuffer = new BufferBlock<PipelineContainer<T>>();
    }
    
    #endregion
    
    #region IPipelineDestination
    
    public ITargetBlock<PipelineContainer<T>> AsTargetBlock()
    {
        return _messageBuffer;
    }
    
    public async Task StartAsync(CancellationTokenSource cancellationToken)
    {
        try
        {
            await ExecuteInternal(cancellationToken);
        }
        catch (Exception e)
        {
            // Exceptions on Destinations can be un-recoverable
            // hence any exceptions shut down the pipeline
            Logger.LogError(e, "Error in the PipelineDestination");
            //cancellationToken.Cancel();
        }
        finally
        {
            //_state = PipelineComponentState.NotRunning;
            //Log.Logger.Error("Shutting down the PipelineSource");
        }
    }

    public Task Completion => _messageBuffer.Completion;

    public void Initialize(Pipeline<T> parentPipeline)
    {
        this._parentPipeline = parentPipeline;
    }

    #endregion
    
    #region Required Implementation

    /// <summary>
    /// This method should contain the logic for the pipeline source.
    /// Source method is async and should contain the complete application loop for the source
    /// </summary>
    /// <returns></returns>
    private async Task ExecuteInternal(CancellationTokenSource cancellationToken)
    {
        await Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sourceRecord = _messageBuffer.Receive(cancellationToken.Token);

                try
                {
                    ExecuteAsync(sourceRecord.Record);
                    
                    // Let the pipeline know that the container record has completed the pipeline
                    _parentPipeline.TriggerPipelineEvent(new PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>>(sourceRecord));
                }
                catch (Exception e)
                {
                    cancellationToken.Cancel();
                    return;
                }
            }
        }, cancellationToken.Token);
    }

    // TEST
    protected abstract void ExecuteAsync(T record);

    #endregion
}
