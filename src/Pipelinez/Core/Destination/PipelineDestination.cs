using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using System.Runtime.ExceptionServices;

namespace Pipelinez.Core.Destination;


public abstract class PipelineDestination<T> : IPipelineDestination<T> where T : PipelineRecord
{
    private readonly BufferBlock<PipelineContainer<T>> _messageBuffer;
    private readonly TaskCompletionSource _completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Pipeline<T>? _parentPipeline;
    protected ILogger<PipelineDestination<T>> Logger { get; }

    #region Constructor
    
    public PipelineDestination()
    {
        Logger = LoggingManager.Instance.CreateLogger<PipelineDestination<T>>();
        _messageBuffer = new BufferBlock<PipelineContainer<T>>();
    }
    
    #endregion
    
    #region IPipelineDestination
    
    public Task Completion => _completionSource.Task;
    
    public ITargetBlock<PipelineContainer<T>> AsTargetBlock()
    {
        return _messageBuffer;
    }
    
    public async Task StartAsync(CancellationTokenSource cancellationToken)
    {
        try
        {
            await ExecuteInternal(cancellationToken).ConfigureAwait(false);
            _completionSource.TrySetResult();
        }
        catch (Exception e)
        {
            // Exceptions on Destinations can be un-recoverable
            // hence any exceptions shut down the pipeline
            Logger.LogError(e, "Error in the PipelineDestination");
            _completionSource.TrySetException(e);
            throw;
        }
        finally
        {
            //_state = PipelineComponentState.NotRunning;
            //Log.Logger.Error("Shutting down the PipelineSource");
        }
    }

    public void Initialize(Pipeline<T> parentPipeline)
    {
        this._parentPipeline = parentPipeline;
        
        // Give a chance for inheritors to initialize
        this.Initialize();
    }

    #endregion
    
    #region Implementation
    
    
    /// <summary>
    /// This method should contain the logic for the pipeline source.
    /// Source method is async and should contain the complete application loop for the source
    /// </summary>
    /// <returns></returns>
    private async Task ExecuteInternal(CancellationTokenSource cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && await _messageBuffer.OutputAvailableAsync().ConfigureAwait(false))
        {
            PipelineContainer<T> sourceRecord;

            try
            {
                sourceRecord = await _messageBuffer.ReceiveAsync(cancellationToken.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (sourceRecord.HasFault)
            {
                var action = await ParentPipeline.HandleFaultedContainerAsync(sourceRecord).ConfigureAwait(false);

                if (action == PipelineErrorAction.SkipRecord)
                {
                    continue;
                }

                if (action == PipelineErrorAction.Rethrow)
                {
                    ExceptionDispatchInfo.Capture(sourceRecord.Fault!.Exception).Throw();
                }

                break;
            }

            try
            {
                await ExecuteAsync(sourceRecord.Record, cancellationToken.Token).ConfigureAwait(false);

                // Let the pipeline know that the container record has completed the pipeline
                ParentPipeline.TriggerPipelineCompletedEvent(
                    new PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>>(sourceRecord));
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in the PipelineDestination");
                sourceRecord.MarkFaulted(new PipelineFaultState(
                    e,
                    GetType().Name,
                    PipelineComponentKind.Destination,
                    DateTimeOffset.UtcNow,
                    e.Message));

                var action = await ParentPipeline.HandleFaultedContainerAsync(sourceRecord).ConfigureAwait(false);

                if (action == PipelineErrorAction.SkipRecord)
                {
                    continue;
                }

                if (action == PipelineErrorAction.Rethrow)
                {
                    ExceptionDispatchInfo.Capture(sourceRecord.Fault!.Exception).Throw();
                }

                break;
            }
        }
        
        Logger.LogInformation("Pipeline Destination has completed");
    }
    
    #endregion
    
    #region Required Implementation
    
    /// <summary>
    /// Execution logic of the destination
    /// </summary>
    /// <param name="record">Record coming through the pipeline</param>
    protected abstract Task ExecuteAsync(T record, CancellationToken cancellationToken);

    /// <summary>
    /// Method to provide an opportunity for the destination to initialize
    /// </summary>
    protected abstract void Initialize();

    #endregion

    private Pipeline<T> ParentPipeline =>
        _parentPipeline ?? throw new InvalidOperationException("Pipeline destination has not been initialized.");
}
