using System.Threading.Tasks.Dataflow;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;
using Pipelinez.Core.Status;

namespace Pipelinez.Core;

public class Pipeline<TPipelineRecord> : IPipeline<TPipelineRecord> where TPipelineRecord : PipelineRecord
{
    private enum PipelineRuntimeState
    {
        NotStarted,
        Starting,
        Running,
        Completing,
        Completed,
        Faulted
    }

    #region Builder
    
    /// <summary>
    /// Initiates the build of a new pipeline with the specified PipelineRecord type
    /// </summary>
    /// <param name="pipelineName">Name of the pipeline</param>
    /// <typeparam name="TPipelineRecord">Type of the record that will flow through the pipeline</typeparam>
    /// <returns></returns>
    public static PipelineBuilder<TPipelineRecord> New(string pipelineName)
    {
        return new PipelineBuilder<TPipelineRecord>(pipelineName);
    }
    
    #endregion
    
    # region Pipeline Components

    private readonly string _name;
    private readonly IPipelineSource<TPipelineRecord> _source;
    private readonly IList<IPipelineSegment<TPipelineRecord>> _segments;
    private readonly IPipelineDestination<TPipelineRecord> _destination;
    private readonly object _stateLock = new();
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private CancellationTokenSource? _runtimeCancellationTokenSource;
    private CancellationTokenRegistration _cancellationRegistration;
    private PipelineRuntimeState _state = PipelineRuntimeState.NotStarted;
    private PipelineFaultState? _pipelineFault;
    
    #endregion
    
    #region Logging
    
    protected ILogger<Pipeline<TPipelineRecord>> Logger { get; }
    
    #endregion
    
    #region Pipeline Execution

    private Task? _sourceExecution;
    private Task? _destinationExecution;
    private Task? _runTask;
    
    #endregion
    
    #region Eventing

    /// <summary>
    /// Occurs when a record in the pipeline has completed traversing the pipeline
    /// </summary>
    public event PipelineRecordCompletedEventHandler<TPipelineRecord>? OnPipelineRecordCompleted;

    /// <summary>
    /// Occurs when a record in the pipeline faults.
    /// </summary>
    public event PipelineRecordFaultedEventHandler<TPipelineRecord>? OnPipelineRecordFaulted;

    /// <summary>
    /// Occurs when the pipeline transitions into a faulted state.
    /// </summary>
    public event PipelineFaultedEventHandler? OnPipelineFaulted;

    /// <summary>
    /// Occurs when the pipeline container has completed traversing the pipeline
    /// </summary>
    internal event PipelineContainerCompletedEventHandler<PipelineContainer<TPipelineRecord>>?
        OnPipelineContainerCompelted; 
    
    /// <summary>
    /// Triggers a PipelineContainerCompleted event
    /// </summary>
    internal void TriggerPipelineCompletedEvent(PipelineContainerCompletedEventHandlerArgs<PipelineContainer<TPipelineRecord>> evt)
    {
        // Container completed does 2 things: 
        // 1 - lets the source know that the record has completed so it can apply any transactional commits if needed
        // 2 - lets pipeline users know that the record has completed
        OnPipelineContainerCompelted?.Invoke(this, evt);
        OnPipelineRecordCompleted?.Invoke(this, new PipelineRecordCompletedEventHandlerArgs<TPipelineRecord>(evt.Container.Record));
    }

    internal void HandleFaultedContainer(PipelineContainer<TPipelineRecord> container)
    {
        Guard.Against.Null(container, nameof(container));

        if (!container.HasFault || container.Fault is null)
        {
            throw new InvalidOperationException("Cannot handle a faulted container without fault state.");
        }

        OnPipelineRecordFaulted?.Invoke(
            this,
            new PipelineRecordFaultedEventArgs<TPipelineRecord>(container.Record, container, container.Fault));

        FaultPipeline(container.Fault);
    }

    #endregion

    #region Construction
    
    internal Pipeline(string name, IPipelineSource<TPipelineRecord> source, IPipelineDestination<TPipelineRecord> destination, 
        IList<IPipelineSegment<TPipelineRecord>> segments)
    {
        Guard.Against.NullOrEmpty(name, message: "Pipeline must have a name");
        Guard.Against.Null(source, message: "Pipeline must have a valid source");
        Guard.Against.Null(destination, message: "Pipeline must have a valid destination");

        this.Logger = LoggingManager.Instance.CreateLogger<Pipeline<TPipelineRecord>>();
        
        this._name = name;
        this._source = source;
        this._destination = destination;
        this._segments = segments;
    }

    internal void LinkPipeline()
    {
        Guard.Against.Null(this._source, message: "Pipeline cannot be initialized. No Source defined");
        Guard.Against.Null(this._destination, message: "Pipeline cannot be initialized. No Destination defined");

        var options =  new DataflowLinkOptions() { MaxMessages = DataflowBlockOptions.Unbounded, PropagateCompletion = true };
        
        if (_segments.Any())
        {
            for (int i = 0; i < this._segments.Count; i++)
            {
                if (i == 0)
                { this._source.ConnectTo(this._segments[i], options); }
                else
                { this._segments[i - 1].ConnectTo(this._segments[i], options); }
            }

            _segments.Last().ConnectTo(_destination, options);
        }
        else
        {
            _source.ConnectTo(_destination, options);
        }

    }

    /// <summary>
    /// Allows for initialization of pipeline components
    /// </summary>
    internal void InitializePipeline()
    {
        // Allow for components of the pipeline to initialize
        _source.Initialize(this);
        _destination.Initialize(this);
    }

    #endregion
    
    // Initiates the pipeline
    public Task StartPipelineAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource runtimeCancellationTokenSource;

        lock (_stateLock)
        {
            if (_state != PipelineRuntimeState.NotStarted)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{_name}' cannot be started while in state '{_state}'.");
            }

            _state = PipelineRuntimeState.Starting;
            runtimeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runtimeCancellationTokenSource = runtimeCancellationTokenSource;
            _cancellationRegistration = runtimeCancellationTokenSource.Token.Register(HandleCancellationRequested);

            try
            {
                _sourceExecution = _source.StartAsync(runtimeCancellationTokenSource);
                _destinationExecution = _destination.StartAsync(runtimeCancellationTokenSource);
                _runTask = MonitorPipelineExecutionAsync(_sourceExecution, _destinationExecution);
                _state = PipelineRuntimeState.Running;
            }
            catch (Exception exception)
            {
                FaultPipeline(CreatePipelineFaultState(
                    exception,
                    _name,
                    PipelineComponentKind.Pipeline,
                    "Pipeline faulted during startup."));
                CleanupRuntimeResources();
                throw;
            }
        }

        Logger.LogInformation("Pipeline started: {PipelineName}", _name);
        return Task.CompletedTask;
    }

    public async Task PublishAsync(TPipelineRecord record)
    {
        Guard.Against.Null(record, nameof(record));
        EnsurePipelineStartedForPublish();

        await _source.PublishAsync(record).ConfigureAwait(false);
    }

    public async Task CompleteAsync()
    {
        Task completionTask = Completion;
        CancellationTokenSource? runtimeCancellationTokenSource = null;

        lock (_stateLock)
        {
            switch (_state)
            {
                case PipelineRuntimeState.NotStarted:
                    throw new InvalidOperationException(
                        $"Pipeline '{_name}' must be started before it can be completed.");
                case PipelineRuntimeState.Running:
                    _state = PipelineRuntimeState.Completing;
                    runtimeCancellationTokenSource = _runtimeCancellationTokenSource;
                    break;
                case PipelineRuntimeState.Starting:
                case PipelineRuntimeState.Completing:
                    runtimeCancellationTokenSource = _runtimeCancellationTokenSource;
                    break;
                case PipelineRuntimeState.Completed:
                case PipelineRuntimeState.Faulted:
                    break;
            }
        }

        if (runtimeCancellationTokenSource is null)
        {
            await completionTask.ConfigureAwait(false);
            return;
        }

        // Mark the source as complete
        _source.Complete();
        // Wait for the completion to propagate to the destination
        await _destination.Completion.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        if (!completionTask.IsCompleted)
        {
            try
            {
                if (!runtimeCancellationTokenSource.IsCancellationRequested)
                {
                    await runtimeCancellationTokenSource.CancelAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException) when (completionTask.IsCompleted)
            {
                // The pipeline finished and cleaned up while completion was in progress.
            }
        }

        await completionTask.ConfigureAwait(false);
    }
    
    /// <summary>Gets a <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation and completion of the pipeline.</summary>
    /// <returns>The task.</returns>
    public Task Completion => _completionSource.Task;

    public PipelineStatus GetStatus()
    {
        var components = new List<PipelineComponentStatus>();
        components.Add(new PipelineComponentStatus(_source.GetType().Name, _source.Completion.Status.ToPipelineExecutionStatus()));
        components.AddRange(_segments.Select(s => new PipelineComponentStatus(s.GetType().Name, s.Completion.Status.ToPipelineExecutionStatus())));
        components.Add(new PipelineComponentStatus(_destination.GetType().Name, _destination.Completion.Status.ToPipelineExecutionStatus()));
        return new PipelineStatus(
            components,
            _state == PipelineRuntimeState.Faulted ? PipelineExecutionStatus.Faulted : null);
    }

    private void EnsurePipelineStartedForPublish()
    {
        lock (_stateLock)
        {
            if (_state != PipelineRuntimeState.Running)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{_name}' must be running before records can be published. Current state: '{_state}'.");
            }
        }
    }

    private void HandleCancellationRequested()
    {
        lock (_stateLock)
        {
            if (_state == PipelineRuntimeState.Running)
            {
                _state = PipelineRuntimeState.Completing;
            }
        }

        try
        {
            _source.Complete();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while completing pipeline source during cancellation.");
        }
    }

    private async Task MonitorPipelineExecutionAsync(Task sourceExecution, Task destinationExecution)
    {
        try
        {
            await Task.WhenAll(sourceExecution, destinationExecution).ConfigureAwait(false);
            TransitionToCompleted();
        }
        catch (Exception exception)
        {
            if (_state != PipelineRuntimeState.Faulted)
            {
                Logger.LogError(exception, "Pipeline execution faulted: {PipelineName}", _name);
                FaultPipeline(CreatePipelineFaultFromTasks(sourceExecution, destinationExecution, exception));
            }
        }
        finally
        {
            CleanupRuntimeResources();
        }
    }

    private void TransitionToCompleted()
    {
        lock (_stateLock)
        {
            if (_state == PipelineRuntimeState.Completed || _state == PipelineRuntimeState.Faulted)
            {
                return;
            }

            _state = PipelineRuntimeState.Completed;
        }

        _completionSource.TrySetResult();
    }

    private void FaultPipeline(PipelineFaultState fault)
    {
        Guard.Against.Null(fault, nameof(fault));

        var shouldRaiseEvent = false;

        lock (_stateLock)
        {
            if (_state == PipelineRuntimeState.Completed || _state == PipelineRuntimeState.Faulted)
            {
                return;
            }

            _state = PipelineRuntimeState.Faulted;
            _pipelineFault = fault;
            shouldRaiseEvent = true;
        }

        _completionSource.TrySetException(fault.Exception);

        if (!shouldRaiseEvent)
        {
            return;
        }

        HandleCancellationRequested();
        RequestRuntimeCancellation();
        OnPipelineFaulted?.Invoke(this, new PipelineFaultedEventArgs(_name, fault));
    }

    private void CleanupRuntimeResources()
    {
        _cancellationRegistration.Dispose();
        _runtimeCancellationTokenSource?.Dispose();
        _runtimeCancellationTokenSource = null;
    }

    private void RequestRuntimeCancellation()
    {
        try
        {
            if (_runtimeCancellationTokenSource is { IsCancellationRequested: false })
            {
                _runtimeCancellationTokenSource.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // The pipeline has already cleaned up its runtime cancellation source.
        }
    }

    private PipelineFaultState CreatePipelineFaultFromTasks(
        Task sourceExecution,
        Task destinationExecution,
        Exception fallbackException)
    {
        if (sourceExecution.Exception?.GetBaseException() is Exception sourceException)
        {
            return CreatePipelineFaultState(
                sourceException,
                _source.GetType().Name,
                PipelineComponentKind.Source);
        }

        if (destinationExecution.Exception?.GetBaseException() is Exception destinationException)
        {
            return CreatePipelineFaultState(
                destinationException,
                _destination.GetType().Name,
                PipelineComponentKind.Destination);
        }

        return CreatePipelineFaultState(
            fallbackException,
            _name,
            PipelineComponentKind.Pipeline);
    }

    private static PipelineFaultState CreatePipelineFaultState(
        Exception exception,
        string componentName,
        PipelineComponentKind componentKind,
        string? message = null)
    {
        return new PipelineFaultState(
            exception,
            componentName,
            componentKind,
            DateTimeOffset.UtcNow,
            message ?? exception.Message);
    }

}






