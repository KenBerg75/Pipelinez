using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks.Dataflow;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Destination;

public abstract class PipelineDestination<T> : IPipelineDestination<T>, IPipelineExecutionConfigurable, IPipelinePerformanceAware, IPipelineBatchingAware
    where T : PipelineRecord
{
    private BufferBlock<PipelineContainer<T>>? _messageBuffer;
    private readonly TaskCompletionSource _completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Pipeline<T>? _parentPipeline;
    private PipelineExecutionOptions _executionOptions = PipelineExecutionOptions.CreateDefaultDestinationOptions();
    private PipelineBatchingOptions? _batchingOptions;
    private IPipelinePerformanceCollector? _performanceCollector;
    private string _componentName = "Destination";

    protected ILogger<PipelineDestination<T>> Logger { get; }

    protected PipelineDestination()
    {
        Logger = LoggingManager.Instance.CreateLogger<PipelineDestination<T>>();
    }

    public Task Completion => _completionSource.Task;

    public ITargetBlock<PipelineContainer<T>> AsTargetBlock()
    {
        return MessageBuffer;
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
            Logger.LogError(e, "Error in the PipelineDestination");
            _completionSource.TrySetException(e);
            throw;
        }
    }

    public void Initialize(Pipeline<T> parentPipeline)
    {
        _parentPipeline = parentPipeline;
        Initialize();
    }

    protected abstract Task ExecuteAsync(T record, CancellationToken cancellationToken);

    protected abstract void Initialize();

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

    void IPipelineBatchingAware.ConfigureBatchingOptions(PipelineBatchingOptions? batchingOptions)
    {
        EnsureExecutionOptionsCanBeChanged();
        _batchingOptions = batchingOptions?.Validate();
    }

    private async Task ExecuteInternal(CancellationTokenSource cancellationToken)
    {
        if (this is IBatchedPipelineDestination<T> batchedDestination && _batchingOptions is not null)
        {
            await ExecuteBatchedInternal(cancellationToken, batchedDestination, _batchingOptions).ConfigureAwait(false);
            Logger.LogInformation("Pipeline Destination has completed");
            return;
        }

        while (!cancellationToken.IsCancellationRequested && await MessageBuffer.OutputAvailableAsync().ConfigureAwait(false))
        {
            PipelineContainer<T> sourceRecord;

            try
            {
                sourceRecord = await MessageBuffer.ReceiveAsync(cancellationToken.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!await TryHandleSuccessfulOrFaultedRecordAsync(sourceRecord, cancellationToken.Token).ConfigureAwait(false))
            {
                break;
            }
        }

        Logger.LogInformation("Pipeline Destination has completed");
    }

    private async Task ExecuteBatchedInternal(
        CancellationTokenSource cancellationToken,
        IBatchedPipelineDestination<T> batchedDestination,
        PipelineBatchingOptions batchingOptions)
    {
        PipelineContainer<T>? bufferedContainer = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var nextContainer = bufferedContainer;
            bufferedContainer = null;

            if (nextContainer is null)
            {
                if (!await MessageBuffer.OutputAvailableAsync().ConfigureAwait(false))
                {
                    break;
                }

                try
                {
                    nextContainer = await MessageBuffer.ReceiveAsync(cancellationToken.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            if (nextContainer.HasFault)
            {
                if (!await HandleFaultedContainerAsync(nextContainer).ConfigureAwait(false))
                {
                    break;
                }

                continue;
            }

            var batch = new List<PipelineContainer<T>>(batchingOptions.BatchSize) { nextContainer };
            var batchDeadline = DateTimeOffset.UtcNow + batchingOptions.MaxBatchDelay;

            while (batch.Count < batchingOptions.BatchSize && !cancellationToken.IsCancellationRequested)
            {
                while (batch.Count < batchingOptions.BatchSize && MessageBuffer.TryReceive(out var candidate))
                {
                    if (candidate.HasFault)
                    {
                        bufferedContainer = candidate;
                        break;
                    }

                    batch.Add(candidate);
                }

                if (bufferedContainer is not null || batch.Count >= batchingOptions.BatchSize)
                {
                    break;
                }

                var remainingDelay = batchDeadline - DateTimeOffset.UtcNow;
                if (remainingDelay <= TimeSpan.Zero)
                {
                    break;
                }

                try
                {
                    var outputAvailableTask = MessageBuffer.OutputAvailableAsync(cancellationToken.Token);
                    var delayTask = Task.Delay(remainingDelay, cancellationToken.Token);
                    var completedTask = await Task.WhenAny(outputAvailableTask, delayTask).ConfigureAwait(false);

                    if (completedTask == delayTask)
                    {
                        break;
                    }

                    if (!outputAvailableTask.Result)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }

            if (batch.Count > 0 && !await ExecuteBatchAsync(batch, batchedDestination, cancellationToken.Token).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    private async Task<bool> TryHandleSuccessfulOrFaultedRecordAsync(
        PipelineContainer<T> sourceRecord,
        CancellationToken cancellationToken)
    {
        if (sourceRecord.HasFault)
        {
            return await HandleFaultedContainerAsync(sourceRecord).ConfigureAwait(false);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await ExecuteAsync(sourceRecord.Record, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _performanceCollector?.RecordComponentExecution(_componentName, stopwatch.Elapsed, succeeded: true);

            ParentPipeline.TriggerPipelineCompletedEvent(
                new PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>>(sourceRecord));
            return true;
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            Logger.LogError(e, "Error in the PipelineDestination");
            _performanceCollector?.RecordComponentExecution(_componentName, stopwatch.Elapsed, succeeded: false);

            sourceRecord.MarkFaulted(new PipelineFaultState(
                e,
                GetType().Name,
                PipelineComponentKind.Destination,
                DateTimeOffset.UtcNow,
                e.Message));

            return await HandleFaultedContainerAsync(sourceRecord).ConfigureAwait(false);
        }
    }

    private async Task<bool> ExecuteBatchAsync(
        IReadOnlyList<PipelineContainer<T>> batch,
        IBatchedPipelineDestination<T> batchedDestination,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await batchedDestination.ExecuteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var durationPerRecord = GetPerRecordDuration(stopwatch.Elapsed, batch.Count);
            foreach (var container in batch)
            {
                _performanceCollector?.RecordComponentExecution(_componentName, durationPerRecord, succeeded: true);
                ParentPipeline.TriggerPipelineCompletedEvent(
                    new PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>>(container));
            }

            return true;
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            Logger.LogError(e, "Error in batched PipelineDestination execution");

            var durationPerRecord = GetPerRecordDuration(stopwatch.Elapsed, batch.Count);

            foreach (var container in batch)
            {
                _performanceCollector?.RecordComponentExecution(_componentName, durationPerRecord, succeeded: false);
                container.MarkFaulted(new PipelineFaultState(
                    e,
                    GetType().Name,
                    PipelineComponentKind.Destination,
                    DateTimeOffset.UtcNow,
                    e.Message));

                if (!await HandleFaultedContainerAsync(container).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private async Task<bool> HandleFaultedContainerAsync(PipelineContainer<T> sourceRecord)
    {
        var action = await ParentPipeline.HandleFaultedContainerAsync(sourceRecord).ConfigureAwait(false);

        if (action == PipelineErrorAction.SkipRecord)
        {
            return true;
        }

        if (action == PipelineErrorAction.Rethrow)
        {
            ExceptionDispatchInfo.Capture(sourceRecord.Fault!.Exception).Throw();
        }

        return false;
    }

    private static TimeSpan GetPerRecordDuration(TimeSpan elapsed, int count)
    {
        return count <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(elapsed.Ticks / count);
    }

    private Pipeline<T> ParentPipeline =>
        _parentPipeline ?? throw new InvalidOperationException("Pipeline destination has not been initialized.");

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
                $"Execution options for destination '{GetType().Name}' must be configured before the destination is linked or used.");
        }
    }
}
