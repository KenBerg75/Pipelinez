using Ardalis.GuardClauses;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Kafka.Client;
using Pipelinez.Kafka.Configuration;
using Pipelinez.Kafka.Record;

namespace Pipelinez.Kafka.Destination;

/// <summary>
/// Publishes pipeline records to a Kafka topic.
/// </summary>
/// <typeparam name="T">The pipeline record type.</typeparam>
/// <typeparam name="TRecordKey">The Kafka message key type.</typeparam>
/// <typeparam name="TRecordValue">The Kafka message value type.</typeparam>
public class KafkaPipelineDestination<T, TRecordKey, TRecordValue> : PipelineDestination<T> where T : PipelineRecord where TRecordKey : class where TRecordValue : class
{
    private readonly string _pipelineName;
    private readonly KafkaDestinationOptions _config;
    private readonly Func<T, Message<TRecordKey, TRecordValue>> _messageMapper;
    private readonly ILogger<KafkaPipelineDestination<T, TRecordKey, TRecordValue>> _logger;
    private IKafkaProducer<TRecordKey, TRecordValue>? _producer;

    /// <summary>
    /// Initializes a new Kafka pipeline destination for the specified pipeline.
    /// </summary>
    /// <param name="pipelineName">The owning pipeline name.</param>
    /// <param name="config">The Kafka destination configuration.</param>
    /// <param name="messageMapper">Maps a pipeline record to the Kafka message to publish.</param>
    public KafkaPipelineDestination(string pipelineName, KafkaDestinationOptions config, Func<T, Message<TRecordKey, TRecordValue>> messageMapper)
    {
        _logger = LoggingManager.Instance.CreateLogger<KafkaPipelineDestination<T, TRecordKey, TRecordValue>>();
        _pipelineName = pipelineName;
        _config = config;
        _messageMapper = messageMapper;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        _logger.LogInformation("Initializing KafkaPipelineDestination");
        // TODO: Inject an IKafkaClientFactory instead of static implementation
        _producer = KafkaClientFactory.CreateProducer<TRecordKey, TRecordValue>(_pipelineName, _config);
        
        // At this point we need a producer or we are dead
        Guard.Against.Null(_producer, nameof(_producer), "No Kafka Producer was created in the destination. Cannot proceed.");
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(T record, CancellationToken cancellationToken)
    {
        _logger.LogTrace("Publishing: {S}", record.ToString());

        var message = _messageMapper(record);
        message.Headers ??= new Headers();

        foreach (var header in record.Headers)
        {
            message.Headers.Add(header.ToKafkaHeader());
        }

        var deliveryResult = await Producer
            .ProduceAsync(_config.TopicName, message, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogTrace(
            "Kafka message delivered to {TopicPartitionOffset}",
            deliveryResult.TopicPartitionOffset);
    }

    private IKafkaProducer<TRecordKey, TRecordValue> Producer =>
        _producer ?? throw new InvalidOperationException("Kafka producer has not been initialized.");

}
