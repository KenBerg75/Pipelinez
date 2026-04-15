using System.Globalization;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Pipelinez.AzureServiceBus.Client;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.AzureServiceBus.Record;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.AzureServiceBus.DeadLettering;

/// <summary>
/// Writes Pipelinez dead-letter records to an Azure Service Bus queue or topic.
/// </summary>
/// <typeparam name="T">The pipeline record type being dead-lettered.</typeparam>
public sealed class AzureServiceBusDeadLetterDestination<T> : IPipelineDeadLetterDestination<T>
    where T : PipelineRecord
{
    private readonly AzureServiceBusDeadLetterOptions _options;
    private readonly Func<PipelineDeadLetterRecord<T>, ServiceBusMessage> _messageMapper;
    private readonly IAzureServiceBusClientFactory _clientFactory;
    private readonly ILogger<AzureServiceBusDeadLetterDestination<T>> _logger;
    private IAzureServiceBusSender? _sender;

    /// <summary>
    /// Initializes a new Azure Service Bus dead-letter destination.
    /// </summary>
    /// <param name="options">The Azure Service Bus dead-letter destination options.</param>
    /// <param name="messageMapper">Maps a Pipelinez dead-letter envelope to the Service Bus message to publish.</param>
    public AzureServiceBusDeadLetterDestination(
        AzureServiceBusDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<T>, ServiceBusMessage> messageMapper)
        : this(options, messageMapper, AzureServiceBusClientFactory.Instance)
    {
    }

    internal AzureServiceBusDeadLetterDestination(
        AzureServiceBusDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<T>, ServiceBusMessage> messageMapper,
        IAzureServiceBusClientFactory clientFactory)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _messageMapper = messageMapper ?? throw new ArgumentNullException(nameof(messageMapper));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = LoggingManager.Instance.CreateLogger<AzureServiceBusDeadLetterDestination<T>>();
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        PipelineDeadLetterRecord<T> deadLetterRecord,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadLetterRecord);

        _sender ??= _clientFactory.CreateDeadLetterSender(_options);

        var message = _messageMapper(deadLetterRecord)
                      ?? throw new InvalidOperationException("Azure Service Bus dead-letter message mapper returned null.");

        deadLetterRecord.Record.CopyHeadersToApplicationProperties(message, _logger);
        AddDeadLetterProperty(message, "pipelinez-deadletter-component", deadLetterRecord.Fault.ComponentName);
        AddDeadLetterProperty(message, "pipelinez-deadletter-kind", deadLetterRecord.Fault.ComponentKind.ToString());
        AddDeadLetterProperty(message, "pipelinez-deadletter-occurred-at", deadLetterRecord.Fault.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AddDeadLetterProperty(message, "pipelinez-deadlettered-at", deadLetterRecord.DeadLetteredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AddDeadLetterProperty(message, "pipelinez-pipeline-source-transport", deadLetterRecord.Distribution.TransportName);
        AddDeadLetterProperty(message, "pipelinez-pipeline-lease-id", deadLetterRecord.Distribution.LeaseId);
        AddDeadLetterProperty(message, "pipelinez-pipeline-offset", deadLetterRecord.Distribution.Offset?.ToString(CultureInfo.InvariantCulture));
        AddDeadLetterProperty(message, DistributedMetadataKeys.TransportName, deadLetterRecord.Distribution.TransportName);
        AddDeadLetterProperty(message, DistributedMetadataKeys.LeaseId, deadLetterRecord.Distribution.LeaseId);

        await _sender
            .SendMessageAsync(message, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogTrace(
            "Pipelinez dead-letter message {MessageId} sent to Azure Service Bus {EntityPath}.",
            message.MessageId,
            _options.Entity.GetEntityPath());
    }

    private static void AddDeadLetterProperty(ServiceBusMessage message, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || message.ApplicationProperties.ContainsKey(key))
        {
            return;
        }

        message.ApplicationProperties[key] = value;
    }
}
