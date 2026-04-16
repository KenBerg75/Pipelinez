using System.Globalization;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Source;
using Pipelinez.RabbitMQ.Client;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Record;

namespace Pipelinez.RabbitMQ.Source;

/// <summary>
/// Consumes records from a RabbitMQ queue and publishes them into a Pipelinez pipeline.
/// </summary>
/// <typeparam name="T">The pipeline record type.</typeparam>
public class RabbitMqPipelineSource<T> : PipelineSourceBase<T>, IDistributedPipelineSource<T>
    where T : PipelineRecord
{
    private static readonly TimeSpan CompletionPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly RabbitMqSourceOptions _options;
    private readonly Func<RabbitMqDeliveryContext, T> _recordMapper;
    private readonly IRabbitMqClientFactory _clientFactory;
    private readonly ILogger<RabbitMqPipelineSource<T>> _logger;
    private readonly object _stateLock = new();
    private readonly Dictionary<string, RabbitMqPendingDelivery> _pendingDeliveries = new(StringComparer.OrdinalIgnoreCase);

    private IRabbitMqChannel? _channel;
    private PipelinePartitionLease? _logicalLease;
    private string? _consumerTag;
    private int _inFlightCount;
    private long? _highestCompletedDeliveryTag;
    private bool _isAssigned;
    private bool _isDraining;

    /// <summary>
    /// Initializes a new RabbitMQ-backed pipeline source.
    /// </summary>
    /// <param name="options">The RabbitMQ source options.</param>
    /// <param name="recordMapper">Maps a RabbitMQ delivery into a pipeline record.</param>
    public RabbitMqPipelineSource(
        RabbitMqSourceOptions options,
        Func<RabbitMqDeliveryContext, T> recordMapper)
        : this(options, recordMapper, RabbitMqClientFactory.Instance)
    {
    }

    internal RabbitMqPipelineSource(
        RabbitMqSourceOptions options,
        Func<RabbitMqDeliveryContext, T> recordMapper,
        IRabbitMqClientFactory clientFactory)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _recordMapper = recordMapper ?? throw new ArgumentNullException(nameof(recordMapper));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = LoggingManager.Instance.CreateLogger<RabbitMqPipelineSource<T>>();
    }

    /// <inheritdoc />
    public bool SupportsDistributedExecution => true;

    /// <inheritdoc />
    public string TransportName => RabbitMqMetadataExtensions.TransportName;

    /// <inheritdoc />
    protected override void Initialize()
    {
        _logger.LogInformation(
            "Initializing RabbitMQ source for queue {QueueName}.",
            _options.Queue.Name);

        _logicalLease = CreateLogicalLease();
        _isAssigned = true;
        ParentPipeline.ReportAssignedPartitions(new[] { _logicalLease });
        ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
    }

    /// <inheritdoc />
    protected override async Task MainLoop(CancellationTokenSource cancellationToken)
    {
        try
        {
            _channel = await _clientFactory
                .CreateSourceChannelAsync(_options, cancellationToken.Token)
                .ConfigureAwait(false);
            await _channel.ConfigureSourceAsync(_options, cancellationToken.Token).ConfigureAwait(false);
            _consumerTag = await _channel
                .BasicConsumeAsync(_options, ProcessDeliveryAsync, cancellationToken.Token)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "RabbitMQ source started for queue {QueueName} with consumer tag {ConsumerTag}.",
                _options.Queue.Name,
                _consumerTag);

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
            await StopConsumerAsync().ConfigureAwait(false);
            await NackPendingDeliveriesAsync().ConfigureAwait(false);
            MarkRevoked();

            if (_channel is not null)
            {
                await _channel.DisposeAsync().ConfigureAwait(false);
            }
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

        ResolvePendingDelivery(settlementKey, RabbitMqSettlementDecision.Ack, canAdvanceProgress: true);
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
            PipelineErrorAction.SkipRecord => RabbitMqSettlementDecision.Ack,
            PipelineErrorAction.DeadLetter => CreateDeadLetterSettlementDecision(),
            PipelineErrorAction.StopPipeline => RabbitMqSettlementDecision.Nack(_options.Settlement.RequeueOnStopOrRethrow),
            PipelineErrorAction.Rethrow => RabbitMqSettlementDecision.Nack(_options.Settlement.RequeueOnStopOrRethrow),
            _ => RabbitMqSettlementDecision.Nack(requeue: true)
        };

        ResolvePendingDelivery(
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

    private async Task ProcessDeliveryAsync(RabbitMqDeliveryContext delivery)
    {
        var settlementKey = GetSettlementKey(delivery.DeliveryTag);
        var pendingDelivery = new RabbitMqPendingDelivery(settlementKey, delivery.DeliveryTag);
        var lease = _logicalLease ?? CreateLogicalLease();

        try
        {
            AddPendingDelivery(pendingDelivery);

            var record = _recordMapper(delivery)
                         ?? throw new InvalidOperationException("RabbitMQ record mapper returned null.");
            delivery.CopyHeadersToRecord(record, _logger);

            var metadata = delivery.ExtractMetadata(_options, lease.LeaseId);
            await PublishAsync(record, metadata).ConfigureAwait(false);

            var decision = await pendingDelivery.Settlement.ConfigureAwait(false);
            await SettleDeliveryAsync(delivery.DeliveryTag, decision).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (RemovePendingDelivery(settlementKey, canAdvanceProgress: false))
            {
                ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
            }

            await TryNackDeliveryAsync(
                    delivery.DeliveryTag,
                    _options.Settlement.RequeueOnPublishAdmissionFailure,
                    "RabbitMQ delivery processing was canceled.")
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (RemovePendingDelivery(settlementKey, canAdvanceProgress: false))
            {
                ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
            }

            _logger.LogError(
                exception,
                "Error processing RabbitMQ delivery {DeliveryTag} from queue {QueueName}.",
                delivery.DeliveryTag,
                _options.Queue.Name);

            await TryNackDeliveryAsync(
                    delivery.DeliveryTag,
                    _options.Settlement.RequeueOnPublishAdmissionFailure,
                    exception.Message)
                .ConfigureAwait(false);
            throw;
        }
    }

    private void AddPendingDelivery(RabbitMqPendingDelivery pendingDelivery)
    {
        lock (_stateLock)
        {
            _pendingDeliveries[pendingDelivery.SettlementKey] = pendingDelivery;
            _inFlightCount++;
        }

        ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
    }

    private void ResolvePendingDelivery(
        string settlementKey,
        RabbitMqSettlementDecision decision,
        bool canAdvanceProgress)
    {
        RabbitMqPendingDelivery? pendingDelivery;

        lock (_stateLock)
        {
            if (!_pendingDeliveries.Remove(settlementKey, out pendingDelivery))
            {
                return;
            }

            if (_inFlightCount > 0)
            {
                _inFlightCount--;
            }

            if (canAdvanceProgress)
            {
                _highestCompletedDeliveryTag = _highestCompletedDeliveryTag.HasValue
                    ? Math.Max(_highestCompletedDeliveryTag.Value, (long)pendingDelivery.DeliveryTag)
                    : (long)pendingDelivery.DeliveryTag;
            }
        }

        pendingDelivery.TrySetSettlement(decision);
        ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
    }

    private bool RemovePendingDelivery(string settlementKey, bool canAdvanceProgress)
    {
        RabbitMqPendingDelivery? pendingDelivery;

        lock (_stateLock)
        {
            if (!_pendingDeliveries.Remove(settlementKey, out pendingDelivery))
            {
                return false;
            }

            if (_inFlightCount > 0)
            {
                _inFlightCount--;
            }

            if (canAdvanceProgress)
            {
                _highestCompletedDeliveryTag = _highestCompletedDeliveryTag.HasValue
                    ? Math.Max(_highestCompletedDeliveryTag.Value, (long)pendingDelivery.DeliveryTag)
                    : (long)pendingDelivery.DeliveryTag;
            }
        }

        pendingDelivery.TryCancel();
        return true;
    }

    private async Task SettleDeliveryAsync(
        ulong deliveryTag,
        RabbitMqSettlementDecision decision)
    {
        var channel = Channel;

        switch (decision.Action)
        {
            case RabbitMqSettlementAction.Ack:
                await channel.BasicAckAsync(deliveryTag, CancellationToken.None).ConfigureAwait(false);
                _logger.LogTrace("Acknowledged RabbitMQ delivery {DeliveryTag}.", deliveryTag);
                break;
            case RabbitMqSettlementAction.Nack:
                await channel.BasicNackAsync(deliveryTag, decision.Requeue, CancellationToken.None).ConfigureAwait(false);
                _logger.LogTrace(
                    "Negatively acknowledged RabbitMQ delivery {DeliveryTag} with requeue {Requeue}.",
                    deliveryTag,
                    decision.Requeue);
                break;
            case RabbitMqSettlementAction.Reject:
                await channel.BasicRejectAsync(deliveryTag, decision.Requeue, CancellationToken.None).ConfigureAwait(false);
                _logger.LogTrace(
                    "Rejected RabbitMQ delivery {DeliveryTag} with requeue {Requeue}.",
                    deliveryTag,
                    decision.Requeue);
                break;
            case RabbitMqSettlementAction.None:
                _logger.LogTrace("Leaving RabbitMQ delivery {DeliveryTag} unsettled.", deliveryTag);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision.Action, "Unknown RabbitMQ settlement action.");
        }
    }

    private async Task TryNackDeliveryAsync(
        ulong deliveryTag,
        bool requeue,
        string reason)
    {
        var channel = _channel;
        if (channel is null || !channel.IsOpen)
        {
            _logger.LogDebug(
                "RabbitMQ channel is closed; delivery {DeliveryTag} will be requeued by the broker if it remains unacknowledged. Reason: {Reason}",
                deliveryTag,
                reason);
            return;
        }

        try
        {
            await channel.BasicNackAsync(deliveryTag, requeue, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "Could not negatively acknowledge RabbitMQ delivery {DeliveryTag}. Reason: {Reason}",
                deliveryTag,
                reason);
        }
    }

    private RabbitMqSettlementDecision CreateDeadLetterSettlementDecision()
    {
        return _options.Settlement.PipelineDeadLetterSettlement == RabbitMqPipelineDeadLetterSettlement.AckSourceMessage
            ? RabbitMqSettlementDecision.Ack
            : RabbitMqSettlementDecision.Nack(requeue: false);
    }

    private async Task StopConsumerAsync()
    {
        if (_channel is null || string.IsNullOrWhiteSpace(_consumerTag))
        {
            return;
        }

        try
        {
            await _channel.BasicCancelAsync(_consumerTag, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Error while canceling RabbitMQ consumer {ConsumerTag}.", _consumerTag);
        }
    }

    private async Task NackPendingDeliveriesAsync()
    {
        RabbitMqPendingDelivery[] pendingDeliveries;

        lock (_stateLock)
        {
            pendingDeliveries = _pendingDeliveries.Values.ToArray();
            _pendingDeliveries.Clear();
            _inFlightCount = 0;
        }

        foreach (var pendingDelivery in pendingDeliveries)
        {
            pendingDelivery.TryCancel();
            await TryNackDeliveryAsync(
                    pendingDelivery.DeliveryTag,
                    requeue: true,
                    "RabbitMQ source is shutting down.")
                .ConfigureAwait(false);
        }
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
            _pendingDeliveries.Clear();
        }

        if (lease is not null)
        {
            ParentPipeline.ReportRevokedPartitions(new[] { lease });
            ParentPipeline.ReportPartitionDrained(lease);
            ParentPipeline.ReportPartitionExecutionStateChanged(CreateExecutionState());
        }
    }

    private PipelinePartitionLease CreateLogicalLease()
    {
        var runtimeContext = ParentPipeline.GetRuntimeContext();
        return new PipelinePartitionLease(
            RabbitMqMetadataExtensions.BuildLeaseId(_options),
            TransportName,
            _options.Queue.Name,
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
                    ?? throw new InvalidOperationException("RabbitMQ logical lease has not been initialized.");
        return new PipelinePartitionExecutionState(
            lease.LeaseId,
            lease.PartitionKey,
            lease.PartitionId,
            _isAssigned,
            _isDraining,
            _inFlightCount,
            _highestCompletedDeliveryTag);
    }

    private IRabbitMqChannel Channel =>
        _channel ?? throw new InvalidOperationException("RabbitMQ source channel has not been initialized.");

    private static string GetSettlementKey(ulong deliveryTag)
    {
        return deliveryTag.ToString(CultureInfo.InvariantCulture);
    }

    private static string? TryGetSettlementKey(MetadataCollection metadata)
    {
        return metadata.GetValue(RabbitMqMetadataKeys.DeliveryTag);
    }
}
