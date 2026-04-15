using Azure.Messaging.ServiceBus;

namespace Pipelinez.AzureServiceBus.Client;

internal interface IAzureServiceBusProcessor : IAsyncDisposable
{
    event Func<ProcessMessageEventArgs, Task> ProcessMessageAsync;

    event Func<ProcessErrorEventArgs, Task> ProcessErrorAsync;

    Task StartProcessingAsync(CancellationToken cancellationToken);

    Task StopProcessingAsync(CancellationToken cancellationToken);
}
