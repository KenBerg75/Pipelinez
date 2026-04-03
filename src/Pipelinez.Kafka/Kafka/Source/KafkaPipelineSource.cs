using Confluent.Kafka;
using Pipelinez.Core.Distributed;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Source;
using Pipelinez.Kafka.Client;
using Pipelinez.Kafka.Configuration;
using Pipelinez.Kafka.Record;

namespace Pipelinez.Kafka.Source;

public class KafkaPipelineSource<T, TRecordKey, TRecordValue> : PipelineSourceBase<T>, IDistributedPipelineSource<T> where T : PipelineRecord where TRecordKey : class where TRecordValue : class
{
    private static readonly TimeSpan ConsumerPollTimeout = TimeSpan.FromMilliseconds(250);
    private readonly string _pipelineName;
    private readonly KafkaSourceOptions _options;
    private readonly Func<TRecordKey, TRecordValue, T> _recordMapper; // Maps Kafka key/value into a pipeline record.
    private IKafkaConsumer<TRecordKey, TRecordValue>? _consumer;
    private readonly ILogger<KafkaPipelineSource<T, TRecordKey, TRecordValue>> _logger;
    private readonly object _leaseLock = new();
    private IReadOnlyList<PipelinePartitionLease> _ownedPartitions = Array.Empty<PipelinePartitionLease>();
    
    public KafkaPipelineSource(string pipelineName, KafkaSourceOptions options, 
        Func<TRecordKey, TRecordValue, T> recordMapper)
    {
        _logger = LoggingManager.Instance.CreateLogger<KafkaPipelineSource<T, TRecordKey, TRecordValue>>();
        _pipelineName = pipelineName;
        _options = options;
        _recordMapper = recordMapper;
    }
    
    protected override void Initialize()
    {
        _logger.LogInformation("Initializing KafkaPipelineSource");
        _consumer = KafkaClientFactory.CreateConsumer<TRecordKey, TRecordValue>(this._pipelineName, this._options);
        Consumer.PartitionsAssigned += HandlePartitionsAssigned;
        Consumer.PartitionsRevoked += HandlePartitionsRevoked;
    }
    
    protected override async Task MainLoop(CancellationTokenSource cancellationToken)
    {
        _logger.LogInformation("Starting KafkaPipelineSource Consumer");
        
        await Task.Run(async () =>
        {
            try
            {
                Consumer.Subscribe(_options.TopicName);
                _logger.LogInformation("KafkaPipelineSource Consumer subscribed to topic [{Topic}]", _options.TopicName);
                
                while (!cancellationToken.IsCancellationRequested && Completion.IsCompleted == false)
                {
                    ConsumeResult<TRecordKey, TRecordValue>? consumeResult;
                    
                    try
                    {
                        _logger.LogTrace("Consume Heartbeat: Name[{@Name}], Sub[{@Subscription}], Id[{@MemberId}]", Consumer.Name, Consumer.Subscription, Consumer.MemberId);
                        consumeResult = Consumer.Consume(ConsumerPollTimeout);
                    }
                    catch (ConsumeException exception)
                    {
                        _logger.LogError(exception, "Error consuming {@Exception}", exception);
                        throw;
                    }

                    if (consumeResult == null || consumeResult.Message == null)
                    {
                        // We got nothing - keep going
                        continue;
                    }

                    try
                    {
                        // Convert the message using the record mapper
                        var record = _recordMapper(consumeResult.Message.Key, consumeResult.Message.Value);
                
                        // Add any headers from the source record
                        if (consumeResult.Message.Headers != null)
                        {
                            foreach (var header in consumeResult.Message.Headers)
                            {
                                record.Headers.Add(header.ToPipelineRecordHeader());
                            }
                        }
                
                        // Note: that if the time spent in this section exceeds the max.poll.timeout
                        // this consumer will get removed from the group and a rebalance will occur
                        await PublishAsync(record, consumeResult.TopicPartitionOffset.ExtractMetadata()).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error processing the record {@Record} from Kafka", consumeResult.Message);
                        throw;
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

                _logger.LogInformation("Dropping out of consume loop");
            }
        });
    }

    
    
    // TODO: Pass this in from configuration
    private readonly int OffsetsStoredReportInterval = 5;
    private long OffsetStored;
    private Dictionary<int, long> OffsetTracker = new();
    
    public override void OnPipelineContainerComplete(object sender, PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e)
    {
        var topicPartitionOffset = TryGetSourceTopicPartitionOffset(e.Container.Metadata);
        if (topicPartitionOffset is null)
        {
            return;
        }

        Consumer.StoreOffset(topicPartitionOffset);
        OffsetTracker[topicPartitionOffset.Partition.Value] = topicPartitionOffset.Offset.Value;
        
        if (++OffsetStored % OffsetsStoredReportInterval == 0)
        {
            _logger.LogInformation("{OffsetStored} records successfully processed and corresponding offsets committed", OffsetStored);
            var offsetReport = OffsetTracker.Aggregate("", (current, kv) => 
                current + $"partition:{kv.Key}->offset[{kv.Value}], ");
            _logger.LogInformation("{Report}", offsetReport.TrimEnd(',', ' '));
        }
    }

    private IKafkaConsumer<TRecordKey, TRecordValue> Consumer =>
        _consumer ?? throw new InvalidOperationException("Kafka consumer has not been initialized.");

    public bool SupportsDistributedExecution => true;

    public string TransportName => "Kafka";

    public IReadOnlyList<PipelinePartitionLease> GetOwnedPartitions()
    {
        lock (_leaseLock)
        {
            return _ownedPartitions.ToArray();
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

        var topicPartition = new TopicPartition(topicName, new Partition(partition));
        return new TopicPartitionOffset(topicPartition, new Offset(offset) + 1);
    }

    private void HandlePartitionsAssigned(IReadOnlyList<TopicPartition> partitions)
    {
        var leases = partitions
            .Select(partition => CreateLease(partition.Topic, partition.Partition.Value))
            .ToArray();

        lock (_leaseLock)
        {
            _ownedPartitions = _ownedPartitions
                .Concat(leases)
                .GroupBy(lease => lease.LeaseId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToArray();
        }

        ParentPipeline.ReportAssignedPartitions(leases);
    }

    private void HandlePartitionsRevoked(IReadOnlyList<TopicPartition> partitions)
    {
        var leases = partitions
            .Select(partition => CreateLease(partition.Topic, partition.Partition.Value))
            .ToArray();

        RemoveOwnedPartitions(leases);

        ParentPipeline.ReportRevokedPartitions(leases);
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
        lock (_leaseLock)
        {
            var ownedPartitions = _ownedPartitions;
            _ownedPartitions = Array.Empty<PipelinePartitionLease>();
            return ownedPartitions;
        }
    }

    private void RemoveOwnedPartitions(IReadOnlyList<PipelinePartitionLease> revokedLeases)
    {
        lock (_leaseLock)
        {
            var revokedIds = revokedLeases
                .Select(lease => lease.LeaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _ownedPartitions = _ownedPartitions
                .Where(lease => !revokedIds.Contains(lease.LeaseId))
                .ToArray();
        }
    }
}
