using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Source;
using Pipelinez.Kafka.Client;
using Pipelinez.Kafka.Configuration;
using Pipelinez.Kafka.Record;

namespace Pipelinez.Kafka.Source;

/// <summary>
/// Consumes records from Kafka and publishes them into a Pipelinez pipeline.
/// </summary>
/// <typeparam name="T">The pipeline record type.</typeparam>
/// <typeparam name="TRecordKey">The Kafka key type.</typeparam>
/// <typeparam name="TRecordValue">The Kafka value type.</typeparam>
public class KafkaPipelineSource<T, TRecordKey, TRecordValue> : PipelineSourceBase<T>, IDistributedPipelineSource<T>
    where T : PipelineRecord
    where TRecordKey : class
    where TRecordValue : class
{
    private enum PendingRecordDecision
    {
        Processed,
        NoneAvailable
    }

    private static readonly TimeSpan ConsumerPollTimeout = TimeSpan.FromMilliseconds(250);

    private readonly string _pipelineName;
    private readonly KafkaSourceOptions _options;
    private readonly KafkaPartitionScalingOptions _partitionScalingOptions;
    private readonly Func<TRecordKey, TRecordValue, T> _recordMapper;
    private readonly ILogger<KafkaPipelineSource<T, TRecordKey, TRecordValue>> _logger;
    private readonly object _stateLock = new();

    private IKafkaConsumer<TRecordKey, TRecordValue>? _consumer;
    private readonly Dictionary<string, KafkaPartitionExecutionTracker> _partitionTrackers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<KafkaPendingPartitionRecord<T>>> _pendingRecords = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<PipelinePartitionLease> _ownedPartitions = Array.Empty<PipelinePartitionLease>();

    private readonly int _offsetsStoredReportInterval = 5;
    private long _offsetsStored;
    private readonly Dictionary<int, long> _offsetTracker = new();

    /// <summary>
    /// Initializes a new Kafka-backed pipeline source.
    /// </summary>
    /// <param name="pipelineName">The owning pipeline name.</param>
    /// <param name="options">The Kafka source configuration.</param>
    /// <param name="recordMapper">Maps Kafka key and value payloads into pipeline records.</param>
    public KafkaPipelineSource(
        string pipelineName,
        KafkaSourceOptions options,
        Func<TRecordKey, TRecordValue, T> recordMapper)
    {
        _logger = LoggingManager.Instance.CreateLogger<KafkaPipelineSource<T, TRecordKey, TRecordValue>>();
        _pipelineName = pipelineName;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _partitionScalingOptions = (_options.PartitionScaling ?? new KafkaPartitionScalingOptions()).Validate();
        _recordMapper = recordMapper ?? throw new ArgumentNullException(nameof(recordMapper));
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        _logger.LogInformation("Initializing KafkaPipelineSource");
        _consumer = KafkaClientFactory.CreateConsumer<TRecordKey, TRecordValue>(_pipelineName, _options);
        Consumer.PartitionsAssigned += HandlePartitionsAssigned;
        Consumer.PartitionsRevoked += HandlePartitionsRevoked;
    }

    /// <inheritdoc />
    protected override async Task MainLoop(CancellationTokenSource cancellationToken)
    {
        _logger.LogInformation("Starting KafkaPipelineSource Consumer");

        await Task.Run(async () =>
        {
            try
            {
                Consumer.Subscribe(_options.TopicName);
                _logger.LogInformation("KafkaPipelineSource Consumer subscribed to topic [{Topic}]", _options.TopicName);

                while (!cancellationToken.IsCancellationRequested && !Completion.IsCompleted)
                {
                    await ResumeEligiblePartitionsAsync().ConfigureAwait(false);

                    if (await TryProcessPendingRecordAsync(cancellationToken.Token).ConfigureAwait(false) == PendingRecordDecision.Processed)
                    {
                        continue;
                    }

                    ConsumeResult<TRecordKey, TRecordValue>? consumeResult;

                    try
                    {
                        _logger.LogTrace(
                            "Consume Heartbeat: Name[{Name}], Sub[{Subscription}], Id[{MemberId}]",
                            Consumer.Name,
                            string.Join(",", Consumer.Subscription),
                            Consumer.MemberId);
                        consumeResult = Consumer.Consume(ConsumerPollTimeout);
                    }
                    catch (ConsumeException exception)
                    {
                        _logger.LogError(exception, "Error consuming Kafka message.");
                        throw;
                    }

                    if (consumeResult?.Message is null)
                    {
                        continue;
                    }

                    var pendingRecord = CreatePendingRecord(consumeResult);
                    var decision = AdmitOrQueueConsumedRecord(pendingRecord, out var partitionsToPause);

                    PausePartitions(partitionsToPause);

                    switch (decision)
                    {
                        case KafkaRecordAdmissionDecision.Drop:
                            continue;
                        case KafkaRecordAdmissionDecision.Buffered:
                            continue;
                        case KafkaRecordAdmissionDecision.ProcessNow:
                            await PublishAdmittedRecordAsync(pendingRecord).ConfigureAwait(false);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown Kafka record admission decision.");
                    }
                }
            }
            finally
            {
                try
                {
                    Consumer.Close();
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Error while closing Kafka consumer during shutdown.");
                }

                var remainingLeases = ClearOwnedPartitions();
                if (remainingLeases.Count > 0)
                {
                    ParentPipeline.ReportRevokedPartitions(remainingLeases);
                }

                _logger.LogInformation("Dropping out of Kafka consume loop");
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void OnPipelineContainerComplete(object sender, PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e)
    {
        var topicPartitionOffset = TryGetSourceTopicPartitionOffset(e.Container.Metadata);
        if (topicPartitionOffset is null)
        {
            return;
        }

        HandleTerminalRecord(topicPartitionOffset, canAdvanceProgress: true);
    }

    internal override void OnPipelineContainerFaultHandled(
        object sender,
        PipelineContainerFaultHandledEventHandlerArgs<PipelineContainer<T>> e)
    {
        var topicPartitionOffset = TryGetSourceTopicPartitionOffset(e.Container.Metadata);
        if (topicPartitionOffset is null)
        {
            return;
        }

        HandleTerminalRecord(
            topicPartitionOffset,
            canAdvanceProgress: e.Action is PipelineErrorAction.SkipRecord or PipelineErrorAction.DeadLetter);
    }

    private IKafkaConsumer<TRecordKey, TRecordValue> Consumer =>
        _consumer ?? throw new InvalidOperationException("Kafka consumer has not been initialized.");

    /// <inheritdoc />
    public bool SupportsDistributedExecution => true;

    /// <inheritdoc />
    public string TransportName => "Kafka";

    /// <inheritdoc />
    public IReadOnlyList<PipelinePartitionLease> GetOwnedPartitions()
    {
        lock (_stateLock)
        {
            return _ownedPartitions.ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PipelinePartitionExecutionState> GetPartitionExecutionStates()
    {
        lock (_stateLock)
        {
            return _partitionTrackers
                .Values
                .OrderBy(tracker => tracker.Lease.PartitionId ?? int.MaxValue)
                .ThenBy(tracker => tracker.Lease.PartitionKey, StringComparer.Ordinal)
                .Select(tracker => tracker.ToPublicState())
                .ToArray();
        }
    }

    private KafkaPendingPartitionRecord<T> CreatePendingRecord(ConsumeResult<TRecordKey, TRecordValue> consumeResult)
    {
        var record = _recordMapper(consumeResult.Message.Key, consumeResult.Message.Value);

        if (consumeResult.Message.Headers is not null)
        {
            foreach (var header in consumeResult.Message.Headers)
            {
                record.Headers.Add(header.ToPipelineRecordHeader());
            }
        }

        return new KafkaPendingPartitionRecord<T>(
            record,
            consumeResult.TopicPartitionOffset.ExtractMetadata(),
            consumeResult.TopicPartitionOffset);
    }

    private async Task PublishAdmittedRecordAsync(KafkaPendingPartitionRecord<T> pendingRecord)
    {
        try
        {
            await PublishAsync(pendingRecord.Record, pendingRecord.Metadata).ConfigureAwait(false);
        }
        catch
        {
            RollbackAdmittedRecord(pendingRecord.TopicPartitionOffset);
            throw;
        }
    }

    private async Task<PendingRecordDecision> TryProcessPendingRecordAsync(CancellationToken cancellationToken)
    {
        KafkaPendingPartitionRecord<T>? pendingRecord = null;
        List<TopicPartition>? partitionsToPause = null;
        PipelinePartitionExecutionState? changedState = null;

        lock (_stateLock)
        {
            foreach (var tracker in GetTrackersInSchedulingOrder())
            {
                var leaseId = tracker.Lease.LeaseId;
                if (!_pendingRecords.TryGetValue(leaseId, out var queue) || queue.Count == 0)
                {
                    continue;
                }

                if (!CanPartitionAcceptLocked(tracker))
                {
                    continue;
                }

                pendingRecord = queue.Dequeue();
                changedState = MarkRecordAdmittedLocked(tracker, pendingRecord.TopicPartitionOffset.Offset.Value);
                partitionsToPause = GetPartitionsToPauseLocked();
                break;
            }
        }

        if (pendingRecord is null)
        {
            return PendingRecordDecision.NoneAvailable;
        }

        PausePartitions(partitionsToPause ?? []);
        if (changedState is not null)
        {
            ParentPipeline.ReportPartitionExecutionStateChanged(changedState);
        }
        await PublishAdmittedRecordAsync(pendingRecord).ConfigureAwait(false);
        return PendingRecordDecision.Processed;
    }

    private KafkaRecordAdmissionDecision AdmitOrQueueConsumedRecord(
        KafkaPendingPartitionRecord<T> pendingRecord,
        out IReadOnlyList<TopicPartition> partitionsToPause)
    {
        var leaseId = KafkaMetadataExtensions.BuildLeaseId(
            pendingRecord.TopicPartitionOffset.Topic,
            pendingRecord.TopicPartitionOffset.Partition.Value);

        KafkaRecordAdmissionDecision decision;
        List<TopicPartition>? partitionsToPauseInternal = null;
        PipelinePartitionExecutionState? changedState = null;

        lock (_stateLock)
        {
            if (!_partitionTrackers.TryGetValue(leaseId, out var tracker) || !tracker.IsAssigned || tracker.IsDraining)
            {
                decision = KafkaRecordAdmissionDecision.Drop;
            }
            else if (CanPartitionAcceptLocked(tracker))
            {
                changedState = MarkRecordAdmittedLocked(tracker, pendingRecord.TopicPartitionOffset.Offset.Value);
                partitionsToPauseInternal = GetPartitionsToPauseLocked();
                decision = KafkaRecordAdmissionDecision.ProcessNow;
            }
            else
            {
                if (!_pendingRecords.TryGetValue(leaseId, out var queue))
                {
                    queue = new Queue<KafkaPendingPartitionRecord<T>>();
                    _pendingRecords[leaseId] = queue;
                }

                queue.Enqueue(pendingRecord);
                partitionsToPauseInternal = GetPartitionsToPauseLocked();
                decision = KafkaRecordAdmissionDecision.Buffered;
            }
        }

        partitionsToPause = partitionsToPauseInternal ?? [];
        if (changedState is not null)
        {
            ParentPipeline.ReportPartitionExecutionStateChanged(changedState);
        }
        return decision;
    }

    private void RollbackAdmittedRecord(TopicPartitionOffset topicPartitionOffset)
    {
        var partitionsToResume = new List<TopicPartition>();
        KafkaPartitionExecutionTracker? tracker = null;

        lock (_stateLock)
        {
            if (!_partitionTrackers.TryGetValue(
                    KafkaMetadataExtensions.BuildLeaseId(topicPartitionOffset.Topic, topicPartitionOffset.Partition.Value),
                    out tracker))
            {
                return;
            }

            if (tracker.InFlightCount > 0)
            {
                tracker.InFlightCount--;
            }

            if (tracker.IsAssigned && tracker.IsPaused && CanPartitionAcceptLocked(tracker))
            {
                tracker.IsPaused = false;
                partitionsToResume.Add(tracker.TopicPartition);
            }
        }

        ResumePartitions(partitionsToResume);

        if (tracker is not null)
        {
            ParentPipeline.ReportPartitionExecutionStateChanged(tracker.ToPublicState());
        }
    }

    private void HandleTerminalRecord(TopicPartitionOffset topicPartitionOffset, bool canAdvanceProgress)
    {
        TopicPartitionOffset? offsetToStore = null;
        List<TopicPartition>? partitionsToResume = null;
        PipelinePartitionLease? drainedPartition = null;
        KafkaPartitionExecutionTracker? tracker = null;

        lock (_stateLock)
        {
            if (!_partitionTrackers.TryGetValue(
                    KafkaMetadataExtensions.BuildLeaseId(topicPartitionOffset.Topic, topicPartitionOffset.Partition.Value),
                    out tracker))
            {
                return;
            }

            if (tracker.InFlightCount > 0)
            {
                tracker.InFlightCount--;
            }

            if (canAdvanceProgress)
            {
                tracker.HighestCompletedOffset = tracker.HighestCompletedOffset.HasValue
                    ? Math.Max(tracker.HighestCompletedOffset.Value, topicPartitionOffset.Offset.Value)
                    : topicPartitionOffset.Offset.Value;
                tracker.CompletedOffsets.Add(topicPartitionOffset.Offset.Value);

                if (!tracker.NextCommitOffset.HasValue)
                {
                    tracker.NextCommitOffset = topicPartitionOffset.Offset.Value;
                }

                while (tracker.NextCommitOffset.HasValue &&
                       tracker.CompletedOffsets.Remove(tracker.NextCommitOffset.Value))
                {
                    tracker.NextCommitOffset++;
                    offsetToStore = new TopicPartitionOffset(
                        tracker.TopicPartition,
                        new Offset(tracker.NextCommitOffset.Value));
                }
            }

            if (tracker.IsAssigned && tracker.IsPaused && CanPartitionAcceptLocked(tracker))
            {
                tracker.IsPaused = false;
                partitionsToResume ??= new List<TopicPartition>();
                partitionsToResume.Add(tracker.TopicPartition);
            }

            if (!tracker.IsAssigned && tracker.InFlightCount == 0)
            {
                drainedPartition = tracker.Lease;
                _partitionTrackers.Remove(tracker.Lease.LeaseId);
                _pendingRecords.Remove(tracker.Lease.LeaseId);
            }
        }

        if (offsetToStore is not null && tracker is { IsAssigned: true })
        {
            Consumer.StoreOffset(offsetToStore);
            _offsetTracker[offsetToStore.Partition.Value] = offsetToStore.Offset.Value;

            if (++_offsetsStored % _offsetsStoredReportInterval == 0)
            {
                _logger.LogInformation(
                    "{OffsetStored} records successfully processed and corresponding offsets committed",
                    _offsetsStored);
                var offsetReport = _offsetTracker.Aggregate(
                    string.Empty,
                    (current, kv) => current + $"partition:{kv.Key}->offset[{kv.Value}], ");
                _logger.LogInformation("{Report}", offsetReport.TrimEnd(',', ' '));
            }
        }

        ResumePartitions(partitionsToResume ?? []);

        if (tracker is not null)
        {
            ParentPipeline.ReportPartitionExecutionStateChanged(tracker.ToPublicState());
        }

        if (drainedPartition is not null)
        {
            ParentPipeline.ReportPartitionDrained(drainedPartition);
        }
    }

    private async Task ResumeEligiblePartitionsAsync()
    {
        List<TopicPartition> partitionsToResume;

        lock (_stateLock)
        {
            partitionsToResume = GetPartitionsToResumeLocked();
            foreach (var partition in partitionsToResume)
            {
                if (_partitionTrackers.TryGetValue(
                        KafkaMetadataExtensions.BuildLeaseId(partition.Topic, partition.Partition.Value),
                        out var tracker))
                {
                    tracker.IsPaused = false;
                }
            }
        }

        ResumePartitions(partitionsToResume);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private List<KafkaPartitionExecutionTracker> GetTrackersInSchedulingOrder()
    {
        return _partitionTrackers
            .Values
            .OrderBy(tracker => tracker.Lease.PartitionId ?? int.MaxValue)
            .ThenBy(tracker => tracker.Lease.PartitionKey, StringComparer.Ordinal)
            .ToList();
    }

    private bool CanPartitionAcceptLocked(KafkaPartitionExecutionTracker tracker)
    {
        if (!tracker.IsAssigned || tracker.IsDraining)
        {
            return false;
        }

        if (tracker.InFlightCount >= _partitionScalingOptions.MaxInFlightPerPartition)
        {
            return false;
        }

        if (tracker.InFlightCount > 0)
        {
            return true;
        }

        return GetActivePartitionCountLocked() < _partitionScalingOptions.MaxConcurrentPartitions;
    }

    private int GetActivePartitionCountLocked()
    {
        return _partitionTrackers.Values.Count(tracker => tracker.InFlightCount > 0);
    }

    private static PipelinePartitionExecutionState MarkRecordAdmittedLocked(KafkaPartitionExecutionTracker tracker, long offset)
    {
        tracker.InFlightCount++;

        if (!tracker.NextCommitOffset.HasValue)
        {
            tracker.NextCommitOffset = offset;
        }

        return tracker.ToPublicState();
    }

    private List<TopicPartition> GetPartitionsToPauseLocked()
    {
        return _partitionTrackers.Values
            .Where(ShouldPausePartitionLocked)
            .Select(tracker =>
            {
                tracker.IsPaused = true;
                return tracker.TopicPartition;
            })
            .Distinct()
            .ToList();
    }

    private List<TopicPartition> GetPartitionsToResumeLocked()
    {
        return _partitionTrackers.Values
            .Where(tracker => tracker.IsPaused && CanPartitionAcceptLocked(tracker))
            .Select(tracker => tracker.TopicPartition)
            .Distinct()
            .ToList();
    }

    private bool ShouldPausePartitionLocked(KafkaPartitionExecutionTracker tracker)
    {
        if (tracker.IsPaused)
        {
            return false;
        }

        if (!tracker.IsAssigned || tracker.IsDraining)
        {
            return true;
        }

        if (tracker.InFlightCount >= _partitionScalingOptions.MaxInFlightPerPartition)
        {
            return true;
        }

        var hasPendingRecords = _pendingRecords.TryGetValue(tracker.Lease.LeaseId, out var queue) && queue.Count > 0;

        return tracker.InFlightCount == 0 &&
               GetActivePartitionCountLocked() >= _partitionScalingOptions.MaxConcurrentPartitions &&
               !hasPendingRecords;
    }

    private void PausePartitions(IReadOnlyList<TopicPartition> partitions)
    {
        if (partitions.Count == 0)
        {
            return;
        }

        try
        {
            Consumer.Pause(partitions);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error pausing Kafka partitions {Partitions}", string.Join(",", partitions));
        }
    }

    private void ResumePartitions(IReadOnlyList<TopicPartition> partitions)
    {
        if (partitions.Count == 0)
        {
            return;
        }

        try
        {
            Consumer.Resume(partitions);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error resuming Kafka partitions {Partitions}", string.Join(",", partitions));
        }
    }

    private void HandlePartitionsAssigned(IReadOnlyList<TopicPartition> partitions)
    {
        var leases = partitions
            .Select(partition => CreateLease(partition.Topic, partition.Partition.Value))
            .ToArray();

        PipelinePartitionExecutionState[] changedStates;

        lock (_stateLock)
        {
            _ownedPartitions = _ownedPartitions
                .Concat(leases)
                .GroupBy(lease => lease.LeaseId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToArray();

            changedStates = partitions
                .Select((partition, index) =>
                {
                    var lease = leases[index];
                    var tracker = new KafkaPartitionExecutionTracker(partition, lease);
                    _partitionTrackers[lease.LeaseId] = tracker;
                    return tracker.ToPublicState();
                })
                .ToArray();
        }

        ParentPipeline.ReportAssignedPartitions(leases);
        foreach (var state in changedStates)
        {
            ParentPipeline.ReportPartitionExecutionStateChanged(state);
        }
    }

    private void HandlePartitionsRevoked(IReadOnlyList<TopicPartition> partitions)
    {
        var leases = partitions
            .Select(partition => CreateLease(partition.Topic, partition.Partition.Value))
            .ToArray();
        var drainingPartitions = new List<PipelinePartitionLease>();
        var drainedPartitions = new List<PipelinePartitionLease>();
        var changedStates = new List<PipelinePartitionExecutionState>();

        lock (_stateLock)
        {
            var revokedIds = leases
                .Select(lease => lease.LeaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _ownedPartitions = _ownedPartitions
                .Where(lease => !revokedIds.Contains(lease.LeaseId))
                .ToArray();

            foreach (var lease in leases)
            {
                _pendingRecords.Remove(lease.LeaseId);

                if (!_partitionTrackers.TryGetValue(lease.LeaseId, out var tracker))
                {
                    continue;
                }

                tracker.IsAssigned = false;

                if (tracker.InFlightCount > 0)
                {
                    tracker.IsDraining = true;
                    drainingPartitions.Add(tracker.Lease);
                    changedStates.Add(tracker.ToPublicState());
                    continue;
                }

                drainedPartitions.Add(tracker.Lease);
                _partitionTrackers.Remove(lease.LeaseId);
            }
        }

        foreach (var draining in drainingPartitions)
        {
            ParentPipeline.ReportPartitionDraining(draining);
        }

        foreach (var state in changedStates)
        {
            ParentPipeline.ReportPartitionExecutionStateChanged(state);
        }

        ParentPipeline.ReportRevokedPartitions(leases);

        foreach (var drained in drainedPartitions)
        {
            ParentPipeline.ReportPartitionDrained(drained);
        }
    }

    private PipelinePartitionLease CreateLease(string topicName, int partitionId)
    {
        var runtimeContext = ParentPipeline.GetRuntimeContext();
        return new PipelinePartitionLease(
            KafkaMetadataExtensions.BuildLeaseId(topicName, partitionId),
            TransportName,
            KafkaMetadataExtensions.BuildPartitionKey(topicName, partitionId),
            runtimeContext.InstanceId,
            runtimeContext.WorkerId,
            partitionId);
    }

    private IReadOnlyList<PipelinePartitionLease> ClearOwnedPartitions()
    {
        lock (_stateLock)
        {
            var ownedPartitions = _ownedPartitions;
            _ownedPartitions = Array.Empty<PipelinePartitionLease>();
            return ownedPartitions;
        }
    }

    private static TopicPartitionOffset? TryGetSourceTopicPartitionOffset(MetadataCollection metadata)
    {
        var topicName = metadata.GetByKey(KafkaMetadataKeys.SOURCE_TOPIC_NAME)?.Value;
        var partitionValue = metadata.GetByKey(KafkaMetadataKeys.SOURCE_PARTITION)?.Value;
        var offsetValue = metadata.GetByKey(KafkaMetadataKeys.SOURCE_OFFSET)?.Value;

        if (string.IsNullOrWhiteSpace(topicName) ||
            !int.TryParse(partitionValue, out var partition) ||
            !long.TryParse(offsetValue, out var offset))
        {
            return null;
        }

        return new TopicPartitionOffset(
            new TopicPartition(topicName, new Partition(partition)),
            new Offset(offset));
    }

    private enum KafkaRecordAdmissionDecision
    {
        ProcessNow,
        Buffered,
        Drop
    }
}
