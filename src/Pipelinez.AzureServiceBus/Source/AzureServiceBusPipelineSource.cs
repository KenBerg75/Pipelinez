using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Pipelinez.AzureServiceBus.Client;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.AzureServiceBus.Record;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Source;

namespace Pipelinez.AzureServiceBus.Source;

/// <summary>
/// Consumes records from an Azure Service Bus queue or topic subscription and publishes them into a Pipelinez pipeline.
/// </summary>
/// <typeparam name="T">The pipeline record type.</typeparam>
public class AzureServiceBusPipelineSource<T> : PipelineSourceBase<T>, IDistributedPipelineSource<T>
    where T : PipelineRecord
{
    private static readonly TimeSpan CompletionPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly AzureServiceBusSourceOptions _options;
    private readonly Func<ServiceBusReceivedMessage, T> _recordMapper;
    private readonly IAzureServiceBusClientFactory _clientFactory;
    private readonly ILogger<AzureServiceBusPipelineSource<T>> _logger;
    private readonly object _stateLock = new();
    private readonly Dictionary<string, AzureServiceBusPendingMessage> _pendingMessages = new(StringComparer.OrdinalIgnoreCase);

    private IAzureServiceBusProcessor? _processor;
    private PipelinePartitionLease? _logicalLease;
    private int _inFlightCount;
    private long? _highestCompletedSequenceNumber;
    private bool _isAssigned;
    private bool _isDraining;

    /// <summary>
    /// Initializes a new Azure Service Bus-backed pipeline source.
    /// </summary>
    /// <param name="options">The Azure Service Bus source options.</param>
    /// <param name="recordMapper">Maps a Service Bus received message into a pipeline record.</param>
    public AzureServiceBusPipelineSource(
        AzureServiceBusSourceOptions options,
        Func<ServiceBusReceivedMessage, T> recordMapper)
        : this(options, recordMapper, AzureServiceBusClientFactory.Instance)
    {
    }

    internal AzureServiceBusPipelineSource(
        AzureServiceBusSourceOptions options,
        Func<ServiceBusReceivedMessage, T> recordMapper,
        IAzureServiceBusClientFactory clientFactory)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _recordMapper = recordMapper ?? throw new ArgumentNullException(nameof(recordMapper));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = LoggingManager.Instance.CreateLogger<AzureServiceBusPipelineSource<T>>();
    }

    /// <inheritdoc />
    public bool SupportsDistributedExecution => true;

    /// <inheritdoc />
    public string TransportName => AzureServiceBusMetadataExtensions.TransportName;

    /// <inheritdoc />
    protected override void Initialize()
    {
        _logger.LogInformation(
            "Initializing Azure Service Bus source for {EntityKind} {EntityPath}.",
            _options.Entity.EntityKind,
            _options.Entity.GetEntityPath());

        _logicalLease = CreateLogicalLease();
        _isAssigned = true;
        ParentPipeline.ReportAssignedPartitions(new[] { _logicalLease });
        ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());

        _processor = _clientFactory.CreateProcessor(_options);
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;
    }

    /// <inheritdoc />
    protected override async Task MainLoop(CancellationTokenSource cancellationToken)
    {
        var processor = Processor;

        try
        {
            await processor.StartProcessingAsync(cancellationToken.Token).ConfigureAwait(false);
            _logger.LogInformation(
                "Azure Service Bus source started for {EntityPath}.",
                _options.Entity.GetEntityPath());

            while (!cancellationToken.IsCancellationRequested && !Completion.IsCompleted)
            {
                await Task.Delay(CompletionPollInterval, cancellationToken.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            MarkDraining();

            try
            {
                await processor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Error while stopping Azure Service Bus processor.");
            }

            CancelPendingMessages();
            MarkRevoked();
            await processor.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override void OnPipelineContainerComplete(
        object sender,
        PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e)
    {
        var settlementKey = TryGetSettlementKey(e.Container.Metadata);
        if (settlementKey is null)
        {
            return;
        }

        ResolvePendingMessage(settlementKey, AzureServiceBusSettlementDecision.Complete, canAdvanceProgress: true);
    }

    internal override void OnPipelineContainerFaultHandled(
        object sender,
        PipelineContainerFaultHandledEventHandlerArgs<PipelineContainer<T>> e)
    {
        var settlementKey = TryGetSettlementKey(e.Container.Metadata);
        if (settlementKey is null)
        {
            return;
        }

        var decision = e.Action switch
        {
            PipelineErrorAction.SkipRecord => AzureServiceBusSettlementDecision.Complete,
            PipelineErrorAction.DeadLetter => CreateDeadLetterSettlementDecision(e.Container),
            PipelineErrorAction.StopPipeline => _options.Settlement.AbandonOnStopOrRethrow
                ? AzureServiceBusSettlementDecision.Abandon
                : AzureServiceBusSettlementDecision.None,
            PipelineErrorAction.Rethrow => _options.Settlement.AbandonOnStopOrRethrow
                ? AzureServiceBusSettlementDecision.Abandon
                : AzureServiceBusSettlementDecision.None,
            _ => AzureServiceBusSettlementDecision.Abandon
        };

        ResolvePendingMessage(
            settlementKey,
            decision,
            canAdvanceProgress: e.Action is PipelineErrorAction.SkipRecord or PipelineErrorAction.DeadLetter);
    }

    /// <inheritdoc />
    public IReadOnlyList<PipelinePartitionLease> GetOwnedPartitions()
    {
        lock (_stateLock)
        {
            return _isAssigned && _logicalLease is not null
                ? new[] { _logicalLease }
                : Array.Empty<PipelinePartitionLease>();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PipelinePartitionExecutionState> GetPartitionExecutionStates()
    {
        lock (_stateLock)
        {
            return _logicalLease is null
                ? Array.Empty<PipelinePartitionExecutionState>()
                : new[] { CreateExecutionStateLocked() };
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        var settlementKey = GetSettlementKey(message);
        var pendingMessage = new AzureServiceBusPendingMessage(settlementKey, message.SequenceNumber);
        var lease = _logicalLease ?? CreateLogicalLease();

        try
        {
            AddPendingMessage(pendingMessage);

            var record = _recordMapper(message)
                         ?? throw new InvalidOperationException("Azure Service Bus record mapper returned null.");
            message.CopyApplicationPropertiesToHeaders(record, _logger);

            var metadata = message.ExtractMetadata(_options.Entity, lease.LeaseId);

            await PublishAsync(record, metadata).ConfigureAwait(false);

            var decision = await pendingMessage.Settlement.ConfigureAwait(false);
            await SettleMessageAsync(args, decision).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await TryAbandonMessageAsync(args, "Azure Service Bus message processing was canceled.").ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (RemovePendingMessage(settlementKey, canAdvanceProgress: false))
            {
                ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
            }

            _logger.LogError(
                exception,
                "Error processing Azure Service Bus message {MessageId} with sequence number {SequenceNumber}.",
                message.MessageId,
                message.SequenceNumber);

            await TryAbandonMessageAsync(args, exception.Message).ConfigureAwait(false);
            throw;
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Azure Service Bus processor error. Source: {ErrorSource}, EntityPath: {EntityPath}, Namespace: {FullyQualifiedNamespace}.",
            args.ErrorSource,
            args.EntityPath,
            args.FullyQualifiedNamespace);
        return Task.CompletedTask;
    }

    private void AddPendingMessage(AzureServiceBusPendingMessage pendingMessage)
    {
        lock (_stateLock)
        {
            _pendingMessages[pendingMessage.SettlementKey] = pendingMessage;
            _inFlightCount++;
        }

        ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
    }

    private void ResolvePendingMessage(
        string settlementKey,
        AzureServiceBusSettlementDecision decision,
        bool canAdvanceProgress)
    {
        AzureServiceBusPendingMessage? pendingMessage;

        lock (_stateLock)
        {
            if (!_pendingMessages.Remove(settlementKey, out pendingMessage))
            {
                return;
            }

            if (_inFlightCount > 0)
            {
                _inFlightCount--;
            }

            if (canAdvanceProgress)
            {
                _highestCompletedSequenceNumber = _highestCompletedSequenceNumber.HasValue
                    ? Math.Max(_highestCompletedSequenceNumber.Value, pendingMessage.SequenceNumber)
                    : pendingMessage.SequenceNumber;
            }
        }

        pendingMessage.TrySetSettlement(decision);
        ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
    }

    private bool RemovePendingMessage(string settlementKey, bool canAdvanceProgress)
    {
        AzureServiceBusPendingMessage? pendingMessage;

        lock (_stateLock)
        {
            if (!_pendingMessages.Remove(settlementKey, out pendingMessage))
            {
                return false;
            }

            if (_inFlightCount > 0)
            {
                _inFlightCount--;
            }

            if (canAdvanceProgress)
            {
                _highestCompletedSequenceNumber = _highestCompletedSequenceNumber.HasValue
                    ? Math.Max(_highestCompletedSequenceNumber.Value, pendingMessage.SequenceNumber)
                    : pendingMessage.SequenceNumber;
            }
        }

        pendingMessage.TryCancel();
        return true;
    }

    private async Task SettleMessageAsync(
        ProcessMessageEventArgs args,
        AzureServiceBusSettlementDecision decision)
    {
        switch (decision.Action)
        {
            case AzureServiceBusSettlementAction.Complete:
                await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                _logger.LogTrace("Completed Azure Service Bus message {MessageId}.", args.Message.MessageId);
                break;
            case AzureServiceBusSettlementAction.Abandon:
                await args.AbandonMessageAsync(args.Message).ConfigureAwait(false);
                _logger.LogTrace("Abandoned Azure Service Bus message {MessageId}.", args.Message.MessageId);
                break;
            case AzureServiceBusSettlementAction.DeadLetter:
                await args
                    .DeadLetterMessageAsync(
                        args.Message,
                        decision.DeadLetterReason ?? "PipelinezDeadLetter",
                        decision.DeadLetterDescription ?? "Pipelinez resolved the record to a dead-letter action.")
                    .ConfigureAwait(false);
                _logger.LogTrace("Dead-lettered Azure Service Bus message {MessageId}.", args.Message.MessageId);
                break;
            case AzureServiceBusSettlementAction.None:
                _logger.LogTrace(
                    "Leaving Azure Service Bus message {MessageId} unsettled by configuration.",
                    args.Message.MessageId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision.Action, "Unknown Azure Service Bus settlement action.");
        }
    }

    private async Task TryAbandonMessageAsync(ProcessMessageEventArgs args, string reason)
    {
        try
        {
            await args.AbandonMessageAsync(args.Message).ConfigureAwait(false);
        }
        catch (ServiceBusException exception) when (exception.Reason is ServiceBusFailureReason.MessageLockLost)
        {
            _logger.LogDebug(
                exception,
                "Could not abandon Azure Service Bus message {MessageId} because its lock was lost. Reason: {Reason}",
                args.Message.MessageId,
                reason);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogDebug(
                exception,
                "Could not abandon Azure Service Bus message {MessageId}. Reason: {Reason}",
                args.Message.MessageId,
                reason);
        }
    }

    private AzureServiceBusSettlementDecision CreateDeadLetterSettlementDecision(PipelineContainer<T> container)
    {
        if (_options.Settlement.PipelineDeadLetterSettlement == AzureServiceBusPipelineDeadLetterSettlement.CompleteSourceMessage)
        {
            return AzureServiceBusSettlementDecision.Complete;
        }

        var reason = container.Fault?.ComponentName ?? "PipelinezDeadLetter";
        var description = container.Fault?.Message ?? "Pipelinez resolved the record to a dead-letter action.";
        return AzureServiceBusSettlementDecision.DeadLetter(reason, description);
    }

    private void MarkDraining()
    {
        lock (_stateLock)
        {
            _isDraining = true;
        }

        var lease = _logicalLease;
        if (lease is not null)
        {
            ParentPipeline.ReportPartitionDraining(lease);
            ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
        }
    }

    private void MarkRevoked()
    {
        PipelinePartitionLease? lease;

        lock (_stateLock)
        {
            lease = _logicalLease;
            _isAssigned = false;
            _isDraining = false;
            _inFlightCount = 0;
            _pendingMessages.Clear();
        }

        if (lease is not null)
        {
            ParentPipeline.ReportRevokedPartitions(new[] { lease });
            ParentPipeline.ReportPartitionDrained(lease);
            ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
        }
    }

    private void CancelPendingMessages()
    {
        AzureServiceBusPendingMessage[] pendingMessages;

        lock (_stateLock)
        {
            pendingMessages = _pendingMessages.Values.ToArray();
            _pendingMessages.Clear();
            _inFlightCount = 0;
        }

        foreach (var pendingMessage in pendingMessages)
        {
            pendingMessage.TryCancel();
        }
    }

    private PipelinePartitionLease CreateLogicalLease()
    {
        var runtimeContext = ParentPipeline.GetRuntimeContext();
        return new PipelinePartitionLease(
            AzureServiceBusMetadataExtensions.BuildLeaseId(_options.Entity),
            TransportName,
            _options.Entity.GetPartitionKey(),
            runtimeContext.InstanceId,
            runtimeContext.WorkerId);
    }

    private PipelinePartitionExecutionState CreateExecutionState()
    {
        lock (_stateLock)
        {
            return CreateExecutionStateLocked();
        }
    }

    private PipelinePartitionExecutionState CreateExecutionStateLocked()
    {
        var lease = _logicalLease
                    ?? throw new InvalidOperationException("Azure Service Bus logical lease has not been initialized.");
        return new PipelinePartitionExecutionState(
            lease.LeaseId,
            lease.PartitionKey,
            lease.PartitionId,
            _isAssigned,
            _isDraining,
            _inFlightCount,
            _highestCompletedSequenceNumber);
    }

    private IAzureServiceBusProcessor Processor =>
        _processor ?? throw new InvalidOperationException("Azure Service Bus processor has not been initialized.");

    private static string GetSettlementKey(ServiceBusReceivedMessage message)
    {
        return !string.IsNullOrWhiteSpace(message.LockToken)
            ? message.LockToken
            : message.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? TryGetSettlementKey(MetadataCollection metadata)
    {
        var lockToken = metadata.GetValue(AzureServiceBusMetadataKeys.LockToken);
        if (!string.IsNullOrWhiteSpace(lockToken))
        {
            return lockToken;
        }

        return metadata.GetValue(AzureServiceBusMetadataKeys.SequenceNumber);
    }
}
