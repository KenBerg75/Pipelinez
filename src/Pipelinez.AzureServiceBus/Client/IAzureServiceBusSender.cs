using Azure.Messaging.ServiceBus;

namespace Pipelinez.AzureServiceBus.Client;

internal interface IAzureServiceBusSender : IAsyncDisposable
{
    Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken);
}
