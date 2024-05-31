using Confluent.Kafka;
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

public class KafkaPipelineSource<T, TRecordKey, TRecordValue> : PipelineSourceBase<T> where T : PipelineRecord where TRecordKey : class where TRecordValue : class
{
    private readonly string _pipelineName;
    private readonly KafkaSourceOptions _options;
    private readonly Func<TRecordKey, TRecordValue, T> _recordMapper;// The Kafka Consumer
    private IKafkaConsumer<TRecordKey, TRecordValue> _consumer;
    private readonly ILogger<KafkaPipelineSource<T, TRecordKey, TRecordValue>> _logger;
    
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
    }
    
    protected override async Task MainLoop(CancellationTokenSource cancellationToken)
    {
        _logger.LogInformation("Starting KafkaPipelineSource Consumer");
        
        await Task.Run(async () =>
        {
            _consumer.Subscribe(_options.TopicName);
            _logger.LogInformation("KafkaPipelineSource Consumer subscribed to topic [{Topic}]", _options.TopicName);
            
            while (!cancellationToken.IsCancellationRequested && Completion.IsCompleted == false)
            {
                var consumeResult = new ConsumeResult<TRecordKey, TRecordValue>();
                
                try
                {
                    _logger.LogTrace("Consume Heartbeat: Name[{@Name}], Sub[{@Subscription}], Id[{@MemberId}]", _consumer.Name, _consumer.Subscription, _consumer.MemberId);
                    consumeResult = _consumer.Consume(TimeSpan.FromSeconds(5));
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
                    await PublishAsync(record, consumeResult.TopicPartitionOffset.ExtractMetadata());
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error processing the record {@Record} from Kafka", consumeResult.Message);
                    throw;
                }
                
            }
            
            _logger.LogInformation("Dropping out of consume loop");
        });
    }

    
    
    // TODO: Pass this in from configuration
    private readonly int OffsetsStoredReportInterval = 5;
    private long OffsetStored;
    private Dictionary<int, long> OffsetTracker = new();
    
    public override void OnPipelineContainerComplete(object sender, PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e)
    {
        // If we don't have the data to commit the transaction, then we must do nothing
        if (e.Container.Metadata.HasKey(KafkaMetadataKeys.SOURCE_TOPIC_NAME) &&
            e.Container.Metadata.HasKey(KafkaMetadataKeys.SOURCE_PARTITION) &&
            e.Container.Metadata.HasKey(KafkaMetadataKeys.SOURCE_OFFSET))

        {
            // TODO: add better support for exception handling in null cases
            string topicName = e.Container.Metadata.GetByKey(KafkaMetadataKeys.SOURCE_TOPIC_NAME)?.Value;
            long offset = long.Parse(e.Container.Metadata.GetByKey(KafkaMetadataKeys.SOURCE_OFFSET)?.Value);
            int partition = int.Parse(e.Container.Metadata.GetByKey(KafkaMetadataKeys.SOURCE_PARTITION)?.Value);


            var topicPartition = new TopicPartition(topicName, new Partition(partition));
            var topicPartitionOffset = new TopicPartitionOffset(topicPartition, new Offset(offset) + 1); // increment
            
            // Store the offset
            _consumer.StoreOffset(topicPartitionOffset);
            OffsetTracker[topicPartitionOffset.Partition.Value] = topicPartitionOffset.Offset.Value;
            
            if (++OffsetStored % OffsetsStoredReportInterval == 0)
            {
                _logger.LogInformation("{OffsetStored} records successfully processed and corresponding offsets committed", OffsetStored);
                var offsetReport = OffsetTracker.Aggregate("", (current, kv) => 
                    current + $"partition:{kv.Key}->offset[{kv.Value}], ");
                _logger.LogInformation("{Report}", offsetReport.TrimEnd(',', ' '));
            }
        }
    }
}