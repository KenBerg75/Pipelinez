using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Flow;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;
using Pipelinez.Core.Retry;

namespace Pipelinez.Core.Segment;

public abstract class PipelineSegment<T> : IPipelineSegment<T>, IPipelineExecutionConfigurable, IPipelinePerformanceAware, IPipelineRetryConfigurable<T> where T : PipelineRecord
{
    private TransformBlock<PipelineContainer<T>, PipelineContainer<T>>? _transformBlock;
    private Pipeline<T>? _parentPipeline;
    private PipelineExecutionOptions _executionOptions = PipelineExecutionOptions.CreateDefaultSegmentOptions();
    private PipelineRetryPolicy<T>? _retryPolicy;
    private IPipelinePerformanceCollector? _performanceCollector;
    private string _componentName = "Segment";

    /// <summary>
    /// Logger for the segment
    /// </summary>
    protected ILogger<PipelineSegment<T>> Logger { get; }

    public PipelineSegment()
    {
        Logger = LoggingManager.Instance.CreateLogger<PipelineSegment<T>>();
    }
    
    #region Initialization
    
    /// <summary>
    /// Initialize the TransformBlock
    /// </summary>
    /// <param name="options"></param>
    private void InitializeTransformBlock()
    {
        Logger.LogTrace("Initializing Segment");
        Func<PipelineContainer<T>, Task<PipelineContainer<T>>> wrapper = ExecuteInternal;
        _transformBlock =  new TransformBlock<PipelineContainer<T>, PipelineContainer<T>>(
            wrapper,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = _executionOptions.BoundedCapacity,
                MaxDegreeOfParallelism = _executionOptions.DegreeOfParallelism,
                EnsureOrdered = _executionOptions.EnsureOrdered
            });
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
        return TransformBlock.LinkTo(target.AsTargetBlock(), options);
    }

    public ITargetBlock<PipelineContainer<T>> AsTargetBlock()
    {
        return TransformBlock;
    }

    
    #endregion
    
    #region Execution
    
    private async Task<PipelineContainer<T>> ExecuteInternal(PipelineContainer<T> arg)
    {
        if (arg.HasFault)
        {
            return arg;
        }

        // Reference the implementation provided by the inheriting class
        Func<T, Task<T>> transformMethod = this.ExecuteAsync;
        var segmentName = GetType().Name;
        var startedAtUtc = DateTimeOffset.UtcNow;
        string? failureMessage = null;
        var succeeded = false;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogTrace("Executing Segment");

            var finalResult = await PipelineRetryExecutor.ExecuteAsync(
                    arg,
                    _retryPolicy,
                    segmentName,
                    PipelineComponentKind.Segment,
                    () => ParentPipeline.GetRuntimeCancellationToken(),
                    async (retryAttempt, exception) =>
                    {
                        arg.AddRetryAttempt(retryAttempt);
                        await ParentPipeline.NotifyRecordRetryingAsync(
                            arg,
                            retryAttempt,
                            _retryPolicy?.MaxAttempts ?? 1,
                            exception).ConfigureAwait(false);
                    },
                    () =>
                    {
                        ParentPipeline.NotifyRetryRecovered();
                        return Task.CompletedTask;
                    },
                    _ => transformMethod(arg.Record))
                .ConfigureAwait(false);

            if (finalResult is null)
            {
                throw new InvalidOperationException(
                    $"Pipeline segment '{segmentName}' returned a null record.");
            }

            arg.Record = finalResult;
            succeeded = true;
            
            Logger.LogTrace("Completed Segment Execution");
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error in pipeline segment block");
            failureMessage = e.Message;
            arg.MarkFaulted(new PipelineFaultState(
                e,
                segmentName,
                PipelineComponentKind.Segment,
                DateTimeOffset.UtcNow,
                e.Message));
        }
        finally
        {
            stopwatch.Stop();
            arg.AddSegmentExecution(new PipelineSegmentExecution(
                segmentName,
                startedAtUtc,
                startedAtUtc.Add(stopwatch.Elapsed),
                succeeded,
                failureMessage));
            _performanceCollector?.RecordComponentExecution(_componentName, stopwatch.Elapsed, succeeded);
        }

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

    public Task Completion => TransformBlock.Completion;

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

    public void ConfigureRetryPolicy(PipelineRetryPolicy<T>? retryPolicy)
    {
        EnsureExecutionOptionsCanBeChanged();
        _retryPolicy = retryPolicy;
    }

    public PipelineRetryPolicy<T>? GetRetryPolicy()
    {
        return _retryPolicy;
    }

    void IPipelinePerformanceAware.ConfigurePerformanceCollector(
        IPipelinePerformanceCollector performanceCollector,
        string componentName)
    {
        _performanceCollector = Guard.Against.Null(performanceCollector, nameof(performanceCollector));
        _componentName = Guard.Against.NullOrWhiteSpace(componentName, nameof(componentName));
    }

    private TransformBlock<PipelineContainer<T>, PipelineContainer<T>> TransformBlock
    {
        get
        {
            if (_transformBlock is null)
            {
                InitializeTransformBlock();
            }

            return _transformBlock!;
        }
    }

    private void EnsureExecutionOptionsCanBeChanged()
    {
        if (_transformBlock is not null)
        {
            throw new InvalidOperationException(
                $"Execution options for segment '{GetType().Name}' must be configured before the segment is linked or used.");
        }
    }

    internal void Initialize(Pipeline<T> parentPipeline)
    {
        _parentPipeline = Guard.Against.Null(parentPipeline, nameof(parentPipeline));
    }

    private Pipeline<T> ParentPipeline =>
        _parentPipeline ?? throw new InvalidOperationException("Pipeline segment has not been initialized.");
}
