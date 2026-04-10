using System.Threading.Tasks.Dataflow;
using System.Runtime.ExceptionServices;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Destination;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Operational;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;
using Pipelinez.Core.Retry;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;
using Pipelinez.Core.Status;

namespace Pipelinez.Core;

/// <summary>
/// Implements the Pipelinez runtime for a specific record type.
/// </summary>
/// <typeparam name="TPipelineRecord">The pipeline record type processed by the runtime.</typeparam>
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
    /// Creates a builder for a new pipeline with the specified name.
    /// </summary>
    /// <param name="pipelineName">The logical name of the pipeline.</param>
    /// <returns>A pipeline builder for the specified record type.</returns>
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
    private readonly PipelineErrorHandler<TPipelineRecord>? _errorHandler;
    private readonly PipelineHostOptions _hostOptions;
    private readonly IPipelineDeadLetterDestination<TPipelineRecord>? _deadLetterDestination;
    private readonly PipelineDeadLetterOptions _deadLetterOptions;
    private readonly PipelineFlowControlOptions _flowControlOptions;
    private readonly PipelineOperationalOptions _operationalOptions;
    private readonly IPipelinePerformanceCollector _performanceCollector;
    private readonly IPipelineMetricsEmitter? _metricsEmitter;
    private readonly bool _emitRetryEvents;
    private readonly string _instanceId;
    private readonly string _workerId;
    private readonly object _stateLock = new();
    private readonly object _distributionLock = new();
    private readonly object _flowControlLock = new();
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private CancellationTokenSource? _runtimeCancellationTokenSource;
    private CancellationTokenRegistration _cancellationRegistration;
    private PipelineRuntimeState _state = PipelineRuntimeState.NotStarted;
    private PipelineFaultState? _pipelineFault;
    private IReadOnlyList<PipelinePartitionLease> _ownedPartitions = Array.Empty<PipelinePartitionLease>();
    private bool _workerStartedRaised;
    private bool _workerStoppingRaised;
    private bool? _lastObservedSaturationWarningState;
    private bool? _lastObservedSaturationState;
    private DateTimeOffset? _lastRecordCompletedAtUtc;
    private DateTimeOffset? _lastDeadLetteredAtUtc;
    
    #endregion
    
    #region Logging
    
    /// <summary>
    /// Gets the logger used by the pipeline runtime.
    /// </summary>
    protected ILogger<Pipeline<TPipelineRecord>> Logger { get; }
    
    #endregion
    
    #region Pipeline Execution

    private Task? _sourceExecution;
    private Task? _destinationExecution;
    private Task? _runTask;
    
    #endregion
    
    #region Eventing

    /// <inheritdoc />
    public event PipelineRecordCompletedEventHandler<TPipelineRecord>? OnPipelineRecordCompleted;

    /// <inheritdoc />
    public event PipelineRecordFaultedEventHandler<TPipelineRecord>? OnPipelineRecordFaulted;

    /// <inheritdoc />
    public event PipelineRecordRetryingEventHandler<TPipelineRecord>? OnPipelineRecordRetrying;
    /// <inheritdoc />
    public event PipelineRecordDeadLetteredEventHandler<TPipelineRecord>? OnPipelineRecordDeadLettered;
    /// <inheritdoc />
    public event PipelineDeadLetterWriteFailedEventHandler<TPipelineRecord>? OnPipelineDeadLetterWriteFailed;
    /// <inheritdoc />
    public event PipelineSaturationChangedEventHandler? OnSaturationChanged;
    /// <inheritdoc />
    public event PipelinePublishRejectedEventHandler<TPipelineRecord>? OnPublishRejected;

    /// <inheritdoc />
    public event PipelineFaultedEventHandler? OnPipelineFaulted;

    /// <inheritdoc />
    public event PipelineWorkerStartedEventHandler? OnWorkerStarted;

    /// <inheritdoc />
    public event PipelinePartitionsAssignedEventHandler? OnPartitionsAssigned;

    /// <inheritdoc />
    public event PipelinePartitionsRevokedEventHandler? OnPartitionsRevoked;

    /// <inheritdoc />
    public event PipelinePartitionDrainingEventHandler? OnPartitionDraining;

    /// <inheritdoc />
    public event PipelinePartitionDrainedEventHandler? OnPartitionDrained;

    /// <inheritdoc />
    public event PipelinePartitionExecutionStateChangedEventHandler? OnPartitionExecutionStateChanged;

    /// <inheritdoc />
    public event PipelineWorkerStoppingEventHandler? OnWorkerStopping;

    /// <summary>
    /// Occurs when the pipeline container has completed traversing the pipeline
    /// </summary>
    internal event PipelineContainerCompletedEventHandler<PipelineContainer<TPipelineRecord>>?
        OnPipelineContainerCompelted; 
    internal event PipelineContainerFaultHandledEventHandler<PipelineContainer<TPipelineRecord>>?
        OnPipelineContainerFaultHandled;
    
    /// <summary>
    /// Triggers a PipelineContainerCompleted event
    /// </summary>
    internal void TriggerPipelineCompletedEvent(PipelineContainerCompletedEventHandlerArgs<PipelineContainer<TPipelineRecord>> evt)
    {
        // Container completed does 2 things: 
        // 1 - lets the source know that the record has completed so it can apply any transactional commits if needed
        // 2 - lets pipeline users know that the record has completed
        OnPipelineContainerCompelted?.Invoke(this, evt);
        _performanceCollector.RecordCompleted(evt.Container.CreatedAtUtc);
        _lastRecordCompletedAtUtc = DateTimeOffset.UtcNow;
        OnPipelineRecordCompleted?.Invoke(
            this,
            new PipelineRecordCompletedEventHandlerArgs<TPipelineRecord>(
                evt.Container.Record,
                BuildDistributionContext(evt.Container.Metadata),
                BuildDiagnosticContext(evt.Container.Metadata)));
        ObserveFlowControlState();
    }

    internal async Task<PipelineErrorAction> HandleFaultedContainerAsync(PipelineContainer<TPipelineRecord> container)
    {
        Guard.Against.Null(container, nameof(container));

        if (!container.HasFault || container.Fault is null)
        {
            throw new InvalidOperationException("Cannot handle a faulted container without fault state.");
        }

        OnPipelineRecordFaulted?.Invoke(
            this,
            new PipelineRecordFaultedEventArgs<TPipelineRecord>(
                container.Record,
                container,
                container.Fault,
                BuildDistributionContext(container.Metadata),
                BuildDiagnosticContext(container.Metadata, container.Fault.ComponentName)));
        if (container.RetryHistory.Count > 0)
        {
            _performanceCollector.RecordRetryExhausted();
        }
        _performanceCollector.RecordFaulted(container.CreatedAtUtc);

        var action = await ResolveErrorActionAsync(container, container.Fault).ConfigureAwait(false);

        switch (action)
        {
            case PipelineErrorAction.SkipRecord:
                Logger.LogInformation(
                    "Skipping faulted record in pipeline {PipelineName}. Component: {ComponentName}",
                    _name,
                    container.Fault.ComponentName);
                OnPipelineContainerFaultHandled?.Invoke(
                    this,
                    new PipelineContainerFaultHandledEventHandlerArgs<PipelineContainer<TPipelineRecord>>(
                        container,
                        PipelineErrorAction.SkipRecord));
                ObserveFlowControlState();
                return PipelineErrorAction.SkipRecord;
            case PipelineErrorAction.DeadLetter:
                await HandleDeadLetterAsync(container, container.Fault).ConfigureAwait(false);
                OnPipelineContainerFaultHandled?.Invoke(
                    this,
                    new PipelineContainerFaultHandledEventHandlerArgs<PipelineContainer<TPipelineRecord>>(
                        container,
                        PipelineErrorAction.DeadLetter));
                ObserveFlowControlState();
                return PipelineErrorAction.DeadLetter;
            case PipelineErrorAction.StopPipeline:
                OnPipelineContainerFaultHandled?.Invoke(
                    this,
                    new PipelineContainerFaultHandledEventHandlerArgs<PipelineContainer<TPipelineRecord>>(
                        container,
                        PipelineErrorAction.StopPipeline));
                FaultPipeline(container.Fault);
                return PipelineErrorAction.StopPipeline;
            case PipelineErrorAction.Rethrow:
                OnPipelineContainerFaultHandled?.Invoke(
                    this,
                    new PipelineContainerFaultHandledEventHandlerArgs<PipelineContainer<TPipelineRecord>>(
                        container,
                        PipelineErrorAction.Rethrow));
                FaultPipeline(container.Fault);
                return PipelineErrorAction.Rethrow;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown pipeline error action.");
        }
    }

    #endregion

    #region Construction
    
    internal Pipeline(
        string name,
        IPipelineSource<TPipelineRecord> source,
        IPipelineDestination<TPipelineRecord> destination,
        IList<IPipelineSegment<TPipelineRecord>> segments,
        PipelineErrorHandler<TPipelineRecord>? errorHandler = null,
        PipelineHostOptions? hostOptions = null,
        IPipelineDeadLetterDestination<TPipelineRecord>? deadLetterDestination = null,
        PipelineDeadLetterOptions? deadLetterOptions = null,
        PipelineOperationalOptions? operationalOptions = null,
        PipelineFlowControlOptions? flowControlOptions = null,
        IPipelinePerformanceCollector? performanceCollector = null,
        bool emitRetryEvents = true)
    {
        Guard.Against.NullOrEmpty(name, message: "Pipeline must have a name");
        Guard.Against.Null(source, message: "Pipeline must have a valid source");
        Guard.Against.Null(destination, message: "Pipeline must have a valid destination");

        this.Logger = LoggingManager.Instance.CreateLogger<Pipeline<TPipelineRecord>>();
        
        this._name = name;
        this._source = source;
        this._destination = destination;
        this._segments = segments;
        this._errorHandler = errorHandler;
        _hostOptions = hostOptions ?? new PipelineHostOptions();
        _deadLetterDestination = deadLetterDestination;
        _deadLetterOptions = (deadLetterOptions ?? new PipelineDeadLetterOptions()).Validate();
        _operationalOptions = (operationalOptions ?? new PipelineOperationalOptions()).Validate();
        _flowControlOptions = (flowControlOptions ?? new PipelineFlowControlOptions()).Validate();
        _performanceCollector = performanceCollector ?? new PipelinePerformanceCollector(new PipelineMetricsOptions());
        _emitRetryEvents = emitRetryEvents;
        _instanceId = string.IsNullOrWhiteSpace(_hostOptions.InstanceId)
            ? Environment.MachineName
            : _hostOptions.InstanceId;
        _workerId = string.IsNullOrWhiteSpace(_hostOptions.WorkerId)
            ? $"{_name}-{Guid.NewGuid():N}"
            : _hostOptions.WorkerId;

        if (_operationalOptions.EnableMetrics)
        {
            _metricsEmitter = new PipelineMetricsEmitter(
                _name,
                _workerId,
                _hostOptions.ExecutionMode,
                () => GetPerformanceSnapshot().RecordsPerSecond,
                GetCurrentHealthState);
            _performanceCollector.ConfigureMetricsEmitter(_metricsEmitter);
        }
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
        foreach (var segment in _segments.OfType<PipelineSegment<TPipelineRecord>>())
        {
            segment.Initialize(this);
        }

        // Allow for components of the pipeline to initialize
        _source.Initialize(this);
        _destination.Initialize(this);
    }

    #endregion
    
    /// <inheritdoc />
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

        Logger.LogInformation(
            "Pipeline started: {PipelineName}, Mode: {ExecutionMode}, InstanceId: {InstanceId}, WorkerId: {WorkerId}",
            _name,
            _hostOptions.ExecutionMode,
            _instanceId,
            _workerId);

        RaiseWorkerStartedIfNeeded();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PublishAsync(TPipelineRecord record)
    {
        var publishResult = await PublishAsync(record, new PipelinePublishOptions()).ConfigureAwait(false);
        publishResult.ThrowIfNotAccepted();
    }

    /// <inheritdoc />
    public async Task<PipelinePublishResult> PublishAsync(
        TPipelineRecord record,
        PipelinePublishOptions options)
    {
        Guard.Against.Null(record, nameof(record));
        EnsurePipelineStartedForPublish();

        return await _source.PublishAsync(record, Guard.Against.Null(options, nameof(options)).Validate()).ConfigureAwait(false);
    }

    /// <inheritdoc />
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

        if (_state == PipelineRuntimeState.Faulted && _pipelineFault is not null)
        {
            ExceptionDispatchInfo.Capture(_pipelineFault.Exception).Throw();
        }
    }
    
    /// <inheritdoc />
    public Task Completion => _completionSource.Task;

    /// <inheritdoc />
    public PipelineStatus GetStatus()
    {
        var flowControlStatus = GetFlowControlStatus();
        PipelineComponentFlowStatus? GetFlow(string name) =>
            flowControlStatus.Components.FirstOrDefault(component => component.Name == name);
        var components = new List<PipelineComponentStatus>();
        components.Add(new PipelineComponentStatus(
            _source.GetType().Name,
            _source.Completion.Status.ToPipelineExecutionStatus(),
            GetFlow(_source.GetType().Name)));
        components.AddRange(_segments.Select(segment => new PipelineComponentStatus(
            segment.GetType().Name,
            segment.Completion.Status.ToPipelineExecutionStatus(),
            GetFlow(segment.GetType().Name))));
        components.Add(new PipelineComponentStatus(
            _destination.GetType().Name,
            _destination.Completion.Status.ToPipelineExecutionStatus(),
            GetFlow(_destination.GetType().Name)));
        return new PipelineStatus(
            components,
            _state == PipelineRuntimeState.Faulted ? PipelineExecutionStatus.Faulted : null,
            GetDistributedStatus(),
            flowControlStatus);
    }

    /// <inheritdoc />
    public PipelineRuntimeContext GetRuntimeContext()
    {
        lock (_distributionLock)
        {
            return new PipelineRuntimeContext(
                _name,
                _hostOptions.ExecutionMode,
                _instanceId,
                _workerId,
                _ownedPartitions,
                GetPartitionExecutionStates());
        }
    }

    /// <inheritdoc />
    public PipelinePerformanceSnapshot GetPerformanceSnapshot()
    {
        return _performanceCollector.CreateSnapshot();
    }

    /// <inheritdoc />
    public PipelineHealthStatus GetHealthStatus()
    {
        var status = GetStatus();
        var performance = GetPerformanceSnapshot();
        var reasons = BuildHealthReasons(status, performance);
        var healthState = DetermineHealthState(performance, reasons);

        return new PipelineHealthStatus(
            _name,
            healthState,
            reasons,
            DateTimeOffset.UtcNow,
            status,
            performance,
            _pipelineFault);
    }

    /// <inheritdoc />
    public PipelineOperationalSnapshot GetOperationalSnapshot()
    {
        var status = GetStatus();
        var performance = GetPerformanceSnapshot();
        var health = GetHealthStatus();

        return new PipelineOperationalSnapshot(
            status,
            performance,
            health,
            DateTimeOffset.UtcNow,
            _pipelineFault,
            _lastRecordCompletedAtUtc,
            _lastDeadLetteredAtUtc);
    }

    internal PipelineFlowControlOptions GetFlowControlOptions()
    {
        return _flowControlOptions;
    }

    internal CancellationToken GetRuntimeCancellationToken()
    {
        return _runtimeCancellationTokenSource?.Token ?? CancellationToken.None;
    }

    internal void NotifyPublishAccepted(TimeSpan waitDuration)
    {
        _performanceCollector.RecordPublishWait(waitDuration);
        ObserveFlowControlState();
    }

    internal void NotifyPublishRejected(
        TPipelineRecord record,
        Pipelinez.Core.Record.Metadata.MetadataCollection metadata,
        PipelinePublishResult publishResult)
    {
        Guard.Against.Null(record, nameof(record));
        Guard.Against.Null(metadata, nameof(metadata));
        Guard.Against.Null(publishResult, nameof(publishResult));

        if (publishResult.WaitDuration > TimeSpan.Zero)
        {
            _performanceCollector.RecordPublishWait(publishResult.WaitDuration);
        }

        _performanceCollector.RecordPublishRejected();
        OnPublishRejected?.Invoke(
            this,
            new PipelinePublishRejectedEventArgs<TPipelineRecord>(
                record,
                publishResult.Reason,
                DateTimeOffset.UtcNow,
                BuildDiagnosticContext(metadata)));
        ObserveFlowControlState();
    }

    internal void ObserveFlowControlState()
    {
        var flowControlStatus = GetFlowControlStatus();
        if (flowControlStatus is null)
        {
            return;
        }

        _performanceCollector.ObserveBufferedCount(flowControlStatus.TotalBufferedCount);

        if (!_flowControlOptions.EmitSaturationEvents)
        {
            return;
        }

        var warningThresholdExceeded = flowControlStatus.SaturationRatio >= _flowControlOptions.SaturationWarningThreshold;
        var shouldRaiseEvent = false;

        lock (_flowControlLock)
        {
            if (_lastObservedSaturationWarningState != warningThresholdExceeded ||
                _lastObservedSaturationState != flowControlStatus.IsSaturated)
            {
                _lastObservedSaturationWarningState = warningThresholdExceeded;
                _lastObservedSaturationState = flowControlStatus.IsSaturated;
                shouldRaiseEvent = true;
            }
        }

        if (shouldRaiseEvent)
        {
            OnSaturationChanged?.Invoke(
                this,
                new PipelineSaturationChangedEventArgs(
                    flowControlStatus.SaturationRatio,
                    flowControlStatus.IsSaturated,
                    DateTimeOffset.UtcNow));
        }
    }

    internal Task NotifyRecordRetryingAsync(
        PipelineContainer<TPipelineRecord> container,
        PipelineRetryAttempt retryAttempt,
        int maxAttempts,
        Exception exception)
    {
        Guard.Against.Null(container, nameof(container));
        Guard.Against.Null(retryAttempt, nameof(retryAttempt));
        Guard.Against.NegativeOrZero(maxAttempts, nameof(maxAttempts));
        Guard.Against.Null(exception, nameof(exception));

        _performanceCollector.RecordRetryAttempt();

        if (!_emitRetryEvents)
        {
            return Task.CompletedTask;
        }

        var fault = CreatePipelineFaultState(
            exception,
            retryAttempt.ComponentName,
            retryAttempt.ComponentKind,
            retryAttempt.Message);

        OnPipelineRecordRetrying?.Invoke(
            this,
            new PipelineRecordRetryingEventArgs<TPipelineRecord>(
                container.Record,
                container,
                fault,
                retryAttempt.AttemptNumber,
                maxAttempts,
                retryAttempt.DelayBeforeNextAttempt,
                BuildDistributionContext(container.Metadata),
                BuildDiagnosticContext(container.Metadata, retryAttempt.ComponentName)));

        return Task.CompletedTask;
    }

    internal void NotifyRetryRecovered()
    {
        _performanceCollector.RecordRetryRecovery();
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

    internal void ReportAssignedPartitions(IReadOnlyList<PipelinePartitionLease> assignedPartitions)
    {
        Guard.Against.Null(assignedPartitions, nameof(assignedPartitions));

        PipelineRuntimeContext runtimeContext;

        lock (_distributionLock)
        {
            var activeLeases = _ownedPartitions
                .Concat(assignedPartitions)
                .GroupBy(lease => lease.LeaseId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToArray();

            _ownedPartitions = activeLeases;
            runtimeContext = new PipelineRuntimeContext(
                _name,
                _hostOptions.ExecutionMode,
                _instanceId,
                _workerId,
                _ownedPartitions,
                GetPartitionExecutionStates());
        }

        if (assignedPartitions.Count > 0)
        {
            _metricsEmitter?.ObserveOwnedPartitionCount(runtimeContext.OwnedPartitions.Count);
            OnPartitionsAssigned?.Invoke(this, new PipelinePartitionsAssignedEventArgs(runtimeContext, assignedPartitions));
        }
    }

    internal void ReportRevokedPartitions(IReadOnlyList<PipelinePartitionLease> revokedPartitions)
    {
        Guard.Against.Null(revokedPartitions, nameof(revokedPartitions));

        PipelineRuntimeContext runtimeContext;

        lock (_distributionLock)
        {
            var revokedLeaseIds = revokedPartitions
                .Select(lease => lease.LeaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _ownedPartitions = _ownedPartitions
                .Where(lease => !revokedLeaseIds.Contains(lease.LeaseId))
                .ToArray();

            runtimeContext = new PipelineRuntimeContext(
                _name,
                _hostOptions.ExecutionMode,
                _instanceId,
                _workerId,
                _ownedPartitions,
                GetPartitionExecutionStates());
        }

        if (revokedPartitions.Count > 0)
        {
            _metricsEmitter?.ObserveOwnedPartitionCount(runtimeContext.OwnedPartitions.Count);
            OnPartitionsRevoked?.Invoke(this, new PipelinePartitionsRevokedEventArgs(runtimeContext, revokedPartitions));
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

        RaiseWorkerStoppingIfNeeded();

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

        lock (_stateLock)
        {
            if (_state == PipelineRuntimeState.Completed || _state == PipelineRuntimeState.Faulted)
            {
                return;
            }

            _state = PipelineRuntimeState.Faulted;
            _pipelineFault = fault;
        }

        HandleCancellationRequested();
        RequestRuntimeCancellation();

        try
        {
            RaisePipelineFaultedEvent(fault);
        }
        finally
        {
            _completionSource.TrySetException(fault.Exception);
        }
    }

    private void CleanupRuntimeResources()
    {
        _cancellationRegistration.Dispose();
        _runtimeCancellationTokenSource?.Dispose();
        _runtimeCancellationTokenSource = null;
        _metricsEmitter?.Dispose();
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

    private async Task<PipelineErrorAction> ResolveErrorActionAsync(
        PipelineContainer<TPipelineRecord> container,
        PipelineFaultState fault)
    {
        if (_errorHandler is null)
        {
            return PipelineErrorAction.StopPipeline;
        }

        try
        {
            var context = new PipelineErrorContext<TPipelineRecord>(
                fault.Exception,
                container,
                fault,
                _runtimeCancellationTokenSource?.Token ?? CancellationToken.None);

            return await _errorHandler(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var handlerFault = CreatePipelineFaultState(
                exception,
                "PipelineErrorHandler",
                PipelineComponentKind.Pipeline,
                exception.Message);

            FaultPipeline(handlerFault);
            throw;
        }
    }

    private async Task HandleDeadLetterAsync(
        PipelineContainer<TPipelineRecord> container,
        PipelineFaultState fault)
    {
        var deadLetterRecord = CreateDeadLetterRecord(container, fault);

        if (_deadLetterDestination is null)
        {
            var configurationException = new InvalidOperationException(
                $"Pipeline '{_name}' resolved a faulted record to '{PipelineErrorAction.DeadLetter}', but no dead-letter destination is configured.");
            var configurationFault = CreatePipelineFaultState(
                configurationException,
                "PipelineDeadLetterDestination",
                PipelineComponentKind.Pipeline,
                configurationException.Message);

            _performanceCollector.RecordDeadLetterFailure();
            RaiseDeadLetterWriteFailed(container.Record, deadLetterRecord, configurationException);
            FaultPipeline(configurationFault);
            throw configurationException;
        }

        try
        {
            await _deadLetterDestination
                .WriteAsync(deadLetterRecord, _runtimeCancellationTokenSource?.Token ?? CancellationToken.None)
                .ConfigureAwait(false);

            _performanceCollector.RecordDeadLettered();
            _lastDeadLetteredAtUtc = DateTimeOffset.UtcNow;
            RaiseDeadLettered(container.Record, deadLetterRecord);
        }
        catch (Exception exception)
        {
            _performanceCollector.RecordDeadLetterFailure();
            RaiseDeadLetterWriteFailed(container.Record, deadLetterRecord, exception);

            if (!_deadLetterOptions.TreatDeadLetterFailureAsPipelineFault)
            {
                Logger.LogWarning(
                    exception,
                    "Dead-letter write failed in pipeline {PipelineName}, but runtime is configured to continue.",
                    _name);
                return;
            }

            var componentName = _deadLetterDestination.GetType().Name;
            var deadLetterFault = CreatePipelineFaultState(
                exception,
                componentName,
                PipelineComponentKind.Destination,
                exception.Message);

            FaultPipeline(deadLetterFault);
            throw;
        }
    }

    private PipelineDistributedStatus GetDistributedStatus()
    {
        lock (_distributionLock)
        {
            return new PipelineDistributedStatus(
                _hostOptions.ExecutionMode,
                _instanceId,
                _workerId,
                _ownedPartitions,
                GetPartitionExecutionStates());
        }
    }

    private PipelineFlowControlStatus GetFlowControlStatus()
    {
        var componentFlows = new List<PipelineComponentFlowStatus>();

        if (_source is IPipelineFlowStatusProvider sourceFlowProvider)
        {
            componentFlows.Add(new PipelineComponentFlowStatus(
                _source.GetType().Name,
                sourceFlowProvider.GetApproximateQueueDepth(),
                sourceFlowProvider.GetBoundedCapacity()));
        }

        foreach (var segment in _segments)
        {
            if (segment is IPipelineFlowStatusProvider segmentFlowProvider)
            {
                componentFlows.Add(new PipelineComponentFlowStatus(
                    segment.GetType().Name,
                    segmentFlowProvider.GetApproximateQueueDepth(),
                    segmentFlowProvider.GetBoundedCapacity()));
            }
        }

        if (_destination is IPipelineFlowStatusProvider destinationFlowProvider)
        {
            componentFlows.Add(new PipelineComponentFlowStatus(
                _destination.GetType().Name,
                destinationFlowProvider.GetApproximateQueueDepth(),
                destinationFlowProvider.GetBoundedCapacity()));
        }

        var totalBufferedCount = componentFlows.Sum(component => component.ApproximateQueueDepth);
        var boundedComponents = componentFlows
            .Where(component => component.BoundedCapacity.HasValue)
            .ToArray();
        int? totalCapacity = boundedComponents.Length == 0
            ? null
            : boundedComponents.Sum(component => component.BoundedCapacity!.Value);
        var boundedBufferedCount = boundedComponents.Sum(component => component.ApproximateQueueDepth);
        var saturationRatio = totalCapacity.HasValue && totalCapacity.Value > 0
            ? Math.Min(1d, (double)boundedBufferedCount / totalCapacity.Value)
            : 0d;
        var isSaturated = componentFlows.Any(component => component.IsSaturated);

        return new PipelineFlowControlStatus(
            _flowControlOptions.OverflowPolicy,
            isSaturated,
            saturationRatio,
            totalBufferedCount,
            totalCapacity,
            componentFlows);
    }

    private PipelineRecordDistributionContext BuildDistributionContext(Pipelinez.Core.Record.Metadata.MetadataCollection metadata)
    {
        return new PipelineRecordDistributionContext(
            _instanceId,
            _workerId,
            metadata.GetValue(DistributedMetadataKeys.TransportName),
            metadata.GetValue(DistributedMetadataKeys.LeaseId),
            metadata.GetValue(DistributedMetadataKeys.PartitionKey),
            TryParseInt(metadata.GetValue(DistributedMetadataKeys.PartitionId)),
            TryParseLong(metadata.GetValue(DistributedMetadataKeys.Offset)));
    }

    private PipelineRecordDiagnosticContext BuildDiagnosticContext(
        Pipelinez.Core.Record.Metadata.MetadataCollection metadata,
        string? componentName = null)
    {
        var correlationId = metadata.GetValue(PipelineOperationalMetadataKeys.CorrelationId);
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
            metadata.Set(PipelineOperationalMetadataKeys.CorrelationId, correlationId);
        }

        return new PipelineRecordDiagnosticContext(
            correlationId,
            _name,
            _instanceId,
            _workerId,
            componentName);
    }

    private PipelineDeadLetterRecord<TPipelineRecord> CreateDeadLetterRecord(
        PipelineContainer<TPipelineRecord> container,
        PipelineFaultState fault)
    {
        var deadLetterRecord = new PipelineDeadLetterRecord<TPipelineRecord>
        {
            Record = container.Record,
            Fault = fault,
            Metadata = _deadLetterOptions.CloneMetadata
                ? container.Metadata.Clone()
                : container.Metadata,
            SegmentHistory = container.SegmentHistory.ToArray(),
            RetryHistory = container.RetryHistory.ToArray(),
            CreatedAtUtc = container.CreatedAtUtc,
            DeadLetteredAtUtc = DateTimeOffset.UtcNow,
            Distribution = BuildDistributionContext(container.Metadata)
        };
        deadLetterRecord.Validate();
        return deadLetterRecord;
    }

    private void RaiseDeadLettered(
        TPipelineRecord record,
        PipelineDeadLetterRecord<TPipelineRecord> deadLetterRecord)
    {
        if (!_deadLetterOptions.EmitDeadLetterEvents)
        {
            return;
        }

        OnPipelineRecordDeadLettered?.Invoke(
            this,
            new PipelineRecordDeadLetteredEventArgs<TPipelineRecord>(
                record,
                deadLetterRecord,
                BuildDiagnosticContext(deadLetterRecord.Metadata, deadLetterRecord.Fault.ComponentName)));
    }

    private void RaiseDeadLetterWriteFailed(
        TPipelineRecord record,
        PipelineDeadLetterRecord<TPipelineRecord> deadLetterRecord,
        Exception exception)
    {
        if (!_deadLetterOptions.EmitDeadLetterEvents)
        {
            return;
        }

        OnPipelineDeadLetterWriteFailed?.Invoke(
            this,
            new PipelineDeadLetterWriteFailedEventArgs<TPipelineRecord>(
                record,
                deadLetterRecord,
                exception,
                BuildDiagnosticContext(deadLetterRecord.Metadata, deadLetterRecord.Fault.ComponentName)));
    }

    private void RaiseWorkerStartedIfNeeded()
    {
        if (_hostOptions.ExecutionMode != PipelineExecutionMode.Distributed)
        {
            return;
        }

        PipelineRuntimeContext? runtimeContext = null;

        lock (_distributionLock)
        {
            if (_workerStartedRaised)
            {
                return;
            }

            _workerStartedRaised = true;
            runtimeContext = new PipelineRuntimeContext(
                _name,
                _hostOptions.ExecutionMode,
                _instanceId,
                _workerId,
                _ownedPartitions,
                GetPartitionExecutionStates());
        }

        OnWorkerStarted?.Invoke(this, new PipelineWorkerStartedEventArgs(runtimeContext));
    }

    private void RaiseWorkerStoppingIfNeeded()
    {
        if (_hostOptions.ExecutionMode != PipelineExecutionMode.Distributed)
        {
            return;
        }

        PipelineRuntimeContext? runtimeContext = null;

        lock (_distributionLock)
        {
            if (_workerStoppingRaised)
            {
                return;
            }

            _workerStoppingRaised = true;
            runtimeContext = new PipelineRuntimeContext(
                _name,
                _hostOptions.ExecutionMode,
                _instanceId,
                _workerId,
                _ownedPartitions,
                GetPartitionExecutionStates());
        }

        OnWorkerStopping?.Invoke(this, new PipelineWorkerStoppingEventArgs(runtimeContext));
    }

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static long? TryParseLong(string? value)
    {
        return long.TryParse(value, out var parsed) ? parsed : null;
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

    private void RaisePipelineFaultedEvent(PipelineFaultState fault)
    {
        var handler = OnPipelineFaulted;
        if (handler is null)
        {
            return;
        }

        try
        {
            handler.Invoke(this, new PipelineFaultedEventArgs(_name, fault));
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "An OnPipelineFaulted subscriber threw while handling a fault in pipeline {PipelineName}.",
                _name);
        }
    }

    internal void ReportPartitionDraining(PipelinePartitionLease partition)
    {
        Guard.Against.Null(partition, nameof(partition));

        OnPartitionDraining?.Invoke(
            this,
            new PipelinePartitionDrainingEventArgs(
                GetRuntimeContext(),
                partition));
    }

    internal void ReportPartitionDrained(PipelinePartitionLease partition)
    {
        Guard.Against.Null(partition, nameof(partition));

        OnPartitionDrained?.Invoke(
            this,
            new PipelinePartitionDrainedEventArgs(
                GetRuntimeContext(),
                partition));
    }

    internal void ReportPartitionExecutionStateChanged(PipelinePartitionExecutionState state)
    {
        Guard.Against.Null(state, nameof(state));

        OnPartitionExecutionStateChanged?.Invoke(
            this,
            new PipelinePartitionExecutionStateChangedEventArgs(
                GetRuntimeContext(),
                state));
    }

    private IReadOnlyList<PipelinePartitionExecutionState> GetPartitionExecutionStates()
    {
        if (_source is IDistributedPipelineSource<TPipelineRecord> distributedSource)
        {
            return distributedSource.GetPartitionExecutionStates();
        }

        return Array.Empty<PipelinePartitionExecutionState>();
    }

    private PipelineHealthState GetCurrentHealthState()
    {
        var status = GetStatus();
        var performance = GetPerformanceSnapshot();
        var reasons = BuildHealthReasons(status, performance);
        return DetermineHealthState(performance, reasons);
    }

    private IReadOnlyList<string> BuildHealthReasons(
        PipelineStatus status,
        PipelinePerformanceSnapshot performance)
    {
        var reasons = new List<string>();

        if (status.FlowControlStatus is { } flow &&
            (flow.IsSaturated || flow.SaturationRatio >= _operationalOptions.SaturationDegradedThreshold))
        {
            reasons.Add("Pipeline is saturated.");
        }

        if (_operationalOptions.RetryExhaustionDegradedThreshold > 0 &&
            performance.RetryExhaustions >= _operationalOptions.RetryExhaustionDegradedThreshold)
        {
            reasons.Add("Retry exhaustion threshold exceeded.");
        }

        if (_operationalOptions.DeadLetterDegradedThreshold > 0 &&
            performance.TotalDeadLetteredCount >= _operationalOptions.DeadLetterDegradedThreshold)
        {
            reasons.Add("Dead-letter threshold exceeded.");
        }

        if (_operationalOptions.PublishRejectionDegradedThreshold > 0 &&
            performance.TotalPublishRejectedCount >= _operationalOptions.PublishRejectionDegradedThreshold)
        {
            reasons.Add("Publish rejection threshold exceeded.");
        }

        var drainingPartitions = status.DistributedStatus?.PartitionExecution.Count(partition => partition.IsDraining) ?? 0;
        if (_operationalOptions.DrainingPartitionDegradedThreshold > 0 &&
            drainingPartitions >= _operationalOptions.DrainingPartitionDegradedThreshold)
        {
            reasons.Add("Draining partition threshold exceeded.");
        }

        if (performance.TotalDeadLetterFailureCount > 0)
        {
            reasons.Add("Dead-letter failures observed.");
        }

        return reasons;
    }

    private PipelineHealthState DetermineHealthState(
        PipelinePerformanceSnapshot performance,
        IReadOnlyList<string> reasons)
    {
        PipelineRuntimeState currentState;
        lock (_stateLock)
        {
            currentState = _state;
        }

        return currentState switch
        {
            PipelineRuntimeState.NotStarted => PipelineHealthState.Starting,
            PipelineRuntimeState.Starting => PipelineHealthState.Starting,
            PipelineRuntimeState.Completed => PipelineHealthState.Completed,
            PipelineRuntimeState.Faulted => PipelineHealthState.Unhealthy,
            _ when performance.TotalDeadLetterFailureCount > 0 => PipelineHealthState.Unhealthy,
            _ when reasons.Count > 0 => PipelineHealthState.Degraded,
            _ => PipelineHealthState.Healthy
        };
    }

}






