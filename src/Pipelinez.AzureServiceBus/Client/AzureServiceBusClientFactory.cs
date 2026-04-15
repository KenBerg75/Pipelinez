using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus.Configuration;

namespace Pipelinez.AzureServiceBus.Client;

internal sealed class AzureServiceBusClientFactory : IAzureServiceBusClientFactory
{
    public static AzureServiceBusClientFactory Instance { get; } = new();

    public IAzureServiceBusSender CreateSender(AzureServiceBusDestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validate();
        var client = CreateClient(validated.Connection);
        var sender = client.CreateSender(validated.Entity.GetEntityPath());
        return new AzureServiceBusSender(client, sender);
    }

    public IAzureServiceBusSender CreateDeadLetterSender(AzureServiceBusDeadLetterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validate();
        var client = CreateClient(validated.Connection);
        var sender = client.CreateSender(validated.Entity.GetEntityPath());
        return new AzureServiceBusSender(client, sender);
    }

    public IAzureServiceBusProcessor CreateProcessor(AzureServiceBusSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validate();
        var client = CreateClient(validated.Connection);
        var processorOptions = CreateProcessorOptions(validated);

        var processor = validated.Entity.EntityKind switch
        {
            AzureServiceBusEntityKind.Queue => client.CreateProcessor(validated.Entity.QueueName!, processorOptions),
            AzureServiceBusEntityKind.TopicSubscription => client.CreateProcessor(
                validated.Entity.TopicName!,
                validated.Entity.SubscriptionName!,
                processorOptions),
            _ => throw new InvalidOperationException(
                $"Azure Service Bus source entity kind '{validated.Entity.EntityKind}' is not supported.")
        };

        return new AzureServiceBusProcessor(client, processor);
    }

    private static ServiceBusClient CreateClient(AzureServiceBusConnectionOptions options)
    {
        options.Validate();

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return options.ClientOptions is null
                ? new ServiceBusClient(options.ConnectionString)
                : new ServiceBusClient(options.ConnectionString, options.ClientOptions);
        }

        return options.ClientOptions is null
            ? new ServiceBusClient(options.FullyQualifiedNamespace, options.Credential)
            : new ServiceBusClient(options.FullyQualifiedNamespace, options.Credential, options.ClientOptions);
    }

    private static ServiceBusProcessorOptions CreateProcessorOptions(AzureServiceBusSourceOptions options)
    {
        var processorOptions = new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = options.MaxConcurrentCalls,
            MaxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration,
            PrefetchCount = options.PrefetchCount,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        };

        options.ConfigureProcessor?.Invoke(processorOptions);

        if (processorOptions.ReceiveMode != ServiceBusReceiveMode.PeekLock)
        {
            throw new InvalidOperationException(
                "Azure Service Bus Pipelinez sources require PeekLock receive mode so messages can be settled after terminal pipeline handling.");
        }

        if (processorOptions.AutoCompleteMessages)
        {
            throw new InvalidOperationException(
                "Azure Service Bus Pipelinez sources require AutoCompleteMessages to be false.");
        }

        return processorOptions;
    }

    private sealed class AzureServiceBusSender : IAzureServiceBusSender
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusSender _sender;

        public AzureServiceBusSender(ServiceBusClient client, ServiceBusSender sender)
        {
            _client = client;
            _sender = sender;
        }

        public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken)
        {
            return _sender.SendMessageAsync(message, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _sender.DisposeAsync().ConfigureAwait(false);
            await _client.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class AzureServiceBusProcessor : IAzureServiceBusProcessor
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusProcessor _processor;

        public AzureServiceBusProcessor(ServiceBusClient client, ServiceBusProcessor processor)
        {
            _client = client;
            _processor = processor;
        }

        public event Func<ProcessMessageEventArgs, Task> ProcessMessageAsync
        {
            add => _processor.ProcessMessageAsync += value;
            remove => _processor.ProcessMessageAsync -= value;
        }

        public event Func<ProcessErrorEventArgs, Task> ProcessErrorAsync
        {
            add => _processor.ProcessErrorAsync += value;
            remove => _processor.ProcessErrorAsync -= value;
        }

        public Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            return _processor.StartProcessingAsync(cancellationToken);
        }

        public Task StopProcessingAsync(CancellationToken cancellationToken)
        {
            return _processor.StopProcessingAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _processor.DisposeAsync().ConfigureAwait(false);
            await _client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
