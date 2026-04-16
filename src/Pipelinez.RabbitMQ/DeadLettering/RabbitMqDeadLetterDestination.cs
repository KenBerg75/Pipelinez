using System.Globalization;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.RabbitMQ.Client;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Destination;
using Pipelinez.RabbitMQ.Record;

namespace Pipelinez.RabbitMQ.DeadLettering;

/// <summary>
/// Writes Pipelinez dead-letter records to RabbitMQ.
/// </summary>
/// <typeparam name="T">The pipeline record type being dead-lettered.</typeparam>
public sealed class RabbitMqDeadLetterDestination<T> : IPipelineDeadLetterDestination<T>
    where T : PipelineRecord
{
    private readonly RabbitMqDeadLetterOptions _options;
    private readonly Func<PipelineDeadLetterRecord<T>, RabbitMqPublishMessage> _messageMapper;
    private readonly IRabbitMqClientFactory _clientFactory;
    private readonly ILogger<RabbitMqDeadLetterDestination<T>> _logger;
    private IRabbitMqChannel? _channel;

    /// <summary>
    /// Initializes a new RabbitMQ dead-letter destination.
    /// </summary>
    /// <param name="options">The RabbitMQ dead-letter destination options.</param>
    /// <param name="messageMapper">Maps a Pipelinez dead-letter envelope to the RabbitMQ message to publish.</param>
    public RabbitMqDeadLetterDestination(
        RabbitMqDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<T>, RabbitMqPublishMessage> messageMapper)
        : this(options, messageMapper, RabbitMqClientFactory.Instance)
    {
    }

    internal RabbitMqDeadLetterDestination(
        RabbitMqDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<T>, RabbitMqPublishMessage> messageMapper,
        IRabbitMqClientFactory clientFactory)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate() as RabbitMqDeadLetterOptions
                   ?? throw new InvalidOperationException("RabbitMQ dead-letter options validation returned an unexpected type.");
        _messageMapper = messageMapper ?? throw new ArgumentNullException(nameof(messageMapper));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = LoggingManager.Instance.CreateLogger<RabbitMqDeadLetterDestination<T>>();
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        PipelineDeadLetterRecord<T> deadLetterRecord,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadLetterRecord);

        var message = _messageMapper(deadLetterRecord)
                      ?? throw new InvalidOperationException("RabbitMQ dead-letter message mapper returned null.");
        var properties = deadLetterRecord.Record.PrepareProperties(message, _options.PersistentMessagesByDefault, _logger);
        AddDeadLetterHeaders(properties, deadLetterRecord);

        var exchange = message.Exchange ?? _options.Exchange;
        var routingKey = message.RoutingKey ?? _options.RoutingKey;
        var mandatory = message.Mandatory ?? _options.Mandatory;

        var channel = await GetOrCreateChannelAsync(cancellationToken).ConfigureAwait(false);
        await channel
            .BasicPublishAsync(
                exchange,
                routingKey,
                mandatory,
                properties,
                message.Body,
                _options.UsePublisherConfirms ? _options.PublisherConfirmTimeout : null,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogTrace(
            "Published Pipelinez dead-letter message to RabbitMQ exchange {Exchange} with routing key {RoutingKey}.",
            exchange,
            routingKey);
    }

    private async Task<IRabbitMqChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            return _channel;
        }

        _channel = await _clientFactory.CreateDestinationChannelAsync(_options, cancellationToken).ConfigureAwait(false);
        await _channel.ConfigureDestinationAsync(_options, cancellationToken).ConfigureAwait(false);
        return _channel;
    }

    private static void AddDeadLetterHeaders(
        global::RabbitMQ.Client.BasicProperties properties,
        PipelineDeadLetterRecord<T> deadLetterRecord)
    {
        properties.AddHeaderIfAbsent("pipelinez-deadletter-component", deadLetterRecord.Fault.ComponentName);
        properties.AddHeaderIfAbsent("pipelinez-deadletter-kind", deadLetterRecord.Fault.ComponentKind.ToString());
        properties.AddHeaderIfAbsent("pipelinez-deadletter-occurred-at", deadLetterRecord.Fault.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        properties.AddHeaderIfAbsent("pipelinez-deadlettered-at", deadLetterRecord.DeadLetteredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        properties.AddHeaderIfAbsent("pipelinez-pipeline-source-transport", deadLetterRecord.Distribution.TransportName);
        properties.AddHeaderIfAbsent("pipelinez-pipeline-lease-id", deadLetterRecord.Distribution.LeaseId);
        properties.AddHeaderIfAbsent("pipelinez-pipeline-offset", deadLetterRecord.Distribution.Offset?.ToString(CultureInfo.InvariantCulture));
        properties.AddHeaderIfAbsent(DistributedMetadataKeys.TransportName, deadLetterRecord.Distribution.TransportName);
        properties.AddHeaderIfAbsent(DistributedMetadataKeys.LeaseId, deadLetterRecord.Distribution.LeaseId);
    }
}
