using Microsoft.Extensions.Logging;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.RabbitMQ.Client;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Record;

namespace Pipelinez.RabbitMQ.Destination;

/// <summary>
/// Publishes pipeline records to RabbitMQ.
/// </summary>
/// <typeparam name="T">The pipeline record type.</typeparam>
public class RabbitMqPipelineDestination<T> : PipelineDestination<T> where T : PipelineRecord
{
    private readonly RabbitMqDestinationOptions _options;
    private readonly Func<T, RabbitMqPublishMessage> _messageMapper;
    private readonly IRabbitMqClientFactory _clientFactory;
    private readonly ILogger<RabbitMqPipelineDestination<T>> _logger;
    private IRabbitMqChannel? _channel;

    /// <summary>
    /// Initializes a new RabbitMQ destination.
    /// </summary>
    /// <param name="options">The RabbitMQ destination options.</param>
    /// <param name="messageMapper">Maps a pipeline record to the RabbitMQ message to publish.</param>
    public RabbitMqPipelineDestination(
        RabbitMqDestinationOptions options,
        Func<T, RabbitMqPublishMessage> messageMapper)
        : this(options, messageMapper, RabbitMqClientFactory.Instance)
    {
    }

    internal RabbitMqPipelineDestination(
        RabbitMqDestinationOptions options,
        Func<T, RabbitMqPublishMessage> messageMapper,
        IRabbitMqClientFactory clientFactory)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _messageMapper = messageMapper ?? throw new ArgumentNullException(nameof(messageMapper));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = LoggingManager.Instance.CreateLogger<RabbitMqPipelineDestination<T>>();
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        _logger.LogInformation(
            "Initializing RabbitMQ destination for exchange {Exchange} and routing key {RoutingKey}.",
            _options.Exchange,
            _options.RoutingKey);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(T record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var message = _messageMapper(record)
                      ?? throw new InvalidOperationException("RabbitMQ message mapper returned null.");
        var properties = record.PrepareProperties(message, _options.PersistentMessagesByDefault, _logger);
        var exchange = message.Exchange ?? _options.Exchange;
        var routingKey = message.RoutingKey ?? _options.RoutingKey;
        var mandatory = message.Mandatory ?? _options.Mandatory;

        if (string.IsNullOrWhiteSpace(exchange) && string.IsNullOrWhiteSpace(routingKey))
        {
            throw new InvalidOperationException("RabbitMQ publish requires a routing key when using the default exchange.");
        }

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
            "Published RabbitMQ message to exchange {Exchange} with routing key {RoutingKey}.",
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
}
