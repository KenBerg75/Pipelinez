using Ardalis.GuardClauses;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Text;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Kafka.Client;
using Pipelinez.Kafka.Configuration;
using Pipelinez.Kafka.Record;

namespace Pipelinez.Kafka.Destination;

/// <summary>
/// Writes Pipelinez dead-letter records to a Kafka topic.
/// </summary>
/// <typeparam name="T">The pipeline record type being dead-lettered.</typeparam>
/// <typeparam name="TRecordKey">The Kafka message key type.</typeparam>
/// <typeparam name="TRecordValue">The Kafka message value type.</typeparam>
public sealed class KafkaDeadLetterDestination<T, TRecordKey, TRecordValue>
    : IPipelineDeadLetterDestination<T>
    where T : PipelineRecord
    where TRecordKey : class
    where TRecordValue : class
{
    private readonly KafkaDestinationOptions _config;
    private readonly Func<PipelineDeadLetterRecord<T>, Message<TRecordKey, TRecordValue>> _messageMapper;
    private readonly ILogger<KafkaDeadLetterDestination<T, TRecordKey, TRecordValue>> _logger;
    private IKafkaProducer<TRecordKey, TRecordValue>? _producer;

    /// <summary>
    /// Initializes a new Kafka dead-letter destination for the specified pipeline.
    /// </summary>
    /// <param name="pipelineName">The owning pipeline name.</param>
    /// <param name="config">The Kafka destination configuration.</param>
    /// <param name="messageMapper">Maps a dead-letter record into the Kafka message to publish.</param>
    public KafkaDeadLetterDestination(
        string pipelineName,
        KafkaDestinationOptions config,
        Func<PipelineDeadLetterRecord<T>, Message<TRecordKey, TRecordValue>> messageMapper)
    {
        _config = Guard.Against.Null(config, nameof(config));
        _messageMapper = Guard.Against.Null(messageMapper, nameof(messageMapper));
        _logger = LoggingManager.Instance.CreateLogger<KafkaDeadLetterDestination<T, TRecordKey, TRecordValue>>();
        _producer = KafkaClientFactory.CreateProducer<TRecordKey, TRecordValue>(
            Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName)),
            _config);
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        PipelineDeadLetterRecord<T> deadLetterRecord,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadLetterRecord);

        var message = _messageMapper(deadLetterRecord);
        message.Headers ??= new Headers();

        foreach (var header in deadLetterRecord.Record.Headers)
        {
            message.Headers.Add(header.ToKafkaHeader());
        }

        message.Headers.Add("pipelinez-deadletter-component", Encoding.UTF8.GetBytes(deadLetterRecord.Fault.ComponentName));
        message.Headers.Add("pipelinez-deadletter-kind", Encoding.UTF8.GetBytes(deadLetterRecord.Fault.ComponentKind.ToString()));
        message.Headers.Add("pipelinez-deadletter-occurred-at", Encoding.UTF8.GetBytes(deadLetterRecord.Fault.OccurredAtUtc.ToString("O")));

        await Producer
            .ProduceAsync(_config.TopicName, message, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogTrace(
            "Dead-letter Kafka message produced to topic {TopicName}",
            _config.TopicName);
    }

    private IKafkaProducer<TRecordKey, TRecordValue> Producer =>
        _producer ?? throw new InvalidOperationException("Kafka dead-letter producer has not been initialized.");
}
