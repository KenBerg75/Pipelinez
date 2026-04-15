using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Pipelinez.AzureServiceBus.Client;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.AzureServiceBus.Record;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.AzureServiceBus.Destination;

/// <summary>
/// Publishes pipeline records to an Azure Service Bus queue or topic.
/// </summary>
/// <typeparam name="T">The pipeline record type.</typeparam>
public class AzureServiceBusPipelineDestination<T> : PipelineDestination<T> where T : PipelineRecord
{
    private readonly AzureServiceBusDestinationOptions _options;
    private readonly Func<T, ServiceBusMessage> _messageMapper;
    private readonly IAzureServiceBusClientFactory _clientFactory;
    private readonly ILogger<AzureServiceBusPipelineDestination<T>> _logger;
    private IAzureServiceBusSender? _sender;

    /// <summary>
    /// Initializes a new Azure Service Bus destination.
    /// </summary>
    /// <param name="options">The Azure Service Bus destination options.</param>
    /// <param name="messageMapper">Maps a pipeline record to the Service Bus message to publish.</param>
    public AzureServiceBusPipelineDestination(
        AzureServiceBusDestinationOptions options,
        Func<T, ServiceBusMessage> messageMapper)
        : this(options, messageMapper, AzureServiceBusClientFactory.Instance)
    {
    }

    internal AzureServiceBusPipelineDestination(
        AzureServiceBusDestinationOptions options,
        Func<T, ServiceBusMessage> messageMapper,
        IAzureServiceBusClientFactory clientFactory)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _messageMapper = messageMapper ?? throw new ArgumentNullException(nameof(messageMapper));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = LoggingManager.Instance.CreateLogger<AzureServiceBusPipelineDestination<T>>();
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        _logger.LogInformation(
            "Initializing Azure Service Bus destination for {EntityKind} {EntityPath}",
            _options.Entity.EntityKind,
            _options.Entity.GetEntityPath());
        _sender = _clientFactory.CreateSender(_options);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(T record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var message = _messageMapper(record)
                      ?? throw new InvalidOperationException("Azure Service Bus message mapper returned null.");

        record.CopyHeadersToApplicationProperties(message, _logger);

        await Sender
            .SendMessageAsync(message, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogTrace(
            "Azure Service Bus message {MessageId} sent to {EntityPath}.",
            message.MessageId,
            _options.Entity.GetEntityPath());
    }

    private IAzureServiceBusSender Sender =>
        _sender ?? throw new InvalidOperationException("Azure Service Bus sender has not been initialized.");
}
