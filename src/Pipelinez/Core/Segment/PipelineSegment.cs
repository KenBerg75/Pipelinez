using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Flow;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Segment;

public abstract class PipelineSegment<T> : IPipelineSegment<T> where T : PipelineRecord
{
    private TransformBlock<PipelineContainer<T>, PipelineContainer<T>> _transformBlock = null;

    /// <summary>
    /// Logger for the segment
    /// </summary>
    protected ILogger<PipelineSegment<T>> Logger { get; }

    public PipelineSegment()
    {
        Logger = LoggingManager.Instance.CreateLogger<PipelineSegment<T>>();
        var finalOptions = new ExecutionDataflowBlockOptions() { BoundedCapacity = 10_000 };
        // _errorProvider = errorHandlerProvider;
        // InitializeErrorHandling(finalOptions);
        InitializeTransformBlock(finalOptions);
    }
    
    #region Initialization
    
    /// <summary>
    /// Initialize the TransformBlock
    /// </summary>
    /// <param name="options"></param>
    private void InitializeTransformBlock(ExecutionDataflowBlockOptions options)
    {
        Logger.LogTrace("Initializing Segment");
        Func<PipelineContainer<T>, Task<PipelineContainer<T>>> wrapper = ExecuteInternal;
        _transformBlock =  new TransformBlock<PipelineContainer<T>, PipelineContainer<T>>(wrapper, options);
    }

    

    #endregion
    
    #region IPipelineSegment

    /// <summary>
    /// Connect this source to the next segment in the pipeline
    /// </summary>
    /// <param name="target">IFlowDestination to connect to</param>
    /// <param name="options"></param>
    /// <returns></returns>
    public IDisposable ConnectTo(IFlowDestination<PipelineContainer<T>> target, DataflowLinkOptions? options = null)
    {
        options ??= new DataflowLinkOptions() { MaxMessages = DataflowBlockOptions.Unbounded };
        return _transformBlock.LinkTo(target.AsTargetBlock(), options);
    }

    public ITargetBlock<PipelineContainer<T>> AsTargetBlock()
    {
        return _transformBlock;
    }

    
    #endregion
    
    #region Execution
    
    private async Task<PipelineContainer<T>> ExecuteInternal(PipelineContainer<T> arg)
    {
        // Reference the implementation provided by the inheriting class
        Func<T, Task<T>> transformMethod = this.ExecuteAsync;
        
        T? finalResult = null;
        
        // Should be the last thing we do before entering the try/catch{}
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Logger.LogTrace("Executing Segment");
            
            // Execute the worker method
            finalResult = await transformMethod(arg.Record);
            
            Logger.LogTrace("Completed Segment Execution");
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error in pipeline segment block");

            // Fault the message.
            // Stops segments from processing
            //sourceRecord.Fault(e);
        }
        finally
        {
            stopwatch.Stop();
            
            // TODO: Move/Refactor this
            // Record that this segment has completed for the record
            //sourceRecord.CompletedSegments.Add(new SegmentExecution(this.Name, stopwatch.ElapsedTicks, sourceRecord.IsFaulted()));
            
            //if (sourceRecord.IsFaulted())
            //{
            //    sourceRecord.ErrorMetadata.FaultedSegment = this.Name;
                // put in DLQ
            //    SendToErrorHandler(sourceRecord);
            //}
        }
        
        // ToDo: This will need to change as pipelinecontainer matures with metadata
        // ToDo: Handle a NULL result
        arg.Record = finalResult;
        return arg;
    }
    
    #endregion
    
    #region Required Implementations
    
    /// <summary>
    /// This method should contain the logic for the pipeline segment.
    /// </summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    public abstract Task<T> ExecuteAsync(T arg);
    
    #endregion

    public Task Completion => _transformBlock.Completion;
}