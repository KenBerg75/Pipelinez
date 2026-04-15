using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus.Client;
using Pipelinez.AzureServiceBus.Configuration;

namespace Pipelinez.AzureServiceBus.Tests.Infrastructure;

internal sealed class FakeAzureServiceBusClientFactory : IAzureServiceBusClientFactory
{
    public FakeAzureServiceBusSender Sender { get; } = new();

    public FakeAzureServiceBusProcessor Processor { get; } = new();

    public IAzureServiceBusSender CreateSender(AzureServiceBusDestinationOptions options)
    {
        return Sender;
    }

    public IAzureServiceBusSender CreateDeadLetterSender(AzureServiceBusDeadLetterOptions options)
    {
        return Sender;
    }

    public IAzureServiceBusProcessor CreateProcessor(AzureServiceBusSourceOptions options)
    {
        return Processor;
    }
}

internal sealed class FakeAzureServiceBusSender : IAzureServiceBusSender
{
    private readonly List<ServiceBusMessage> _messages = new();

    public IReadOnlyList<ServiceBusMessage> Messages => _messages.ToArray();

    public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeAzureServiceBusProcessor : IAzureServiceBusProcessor
{
    public event Func<ProcessMessageEventArgs, Task>? ProcessMessageAsync;

    public event Func<ProcessErrorEventArgs, Task>? ProcessErrorAsync;

    public bool Started { get; private set; }

    public bool Stopped { get; private set; }

    public Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        Started = true;
        return Task.CompletedTask;
    }

    public Task StopProcessingAsync(CancellationToken cancellationToken)
    {
        Stopped = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public bool HasMessageHandler => ProcessMessageAsync is not null;

    public bool HasErrorHandler => ProcessErrorAsync is not null;
}
