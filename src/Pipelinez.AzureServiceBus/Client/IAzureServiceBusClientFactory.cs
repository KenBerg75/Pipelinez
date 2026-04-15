using Pipelinez.AzureServiceBus.Configuration;

namespace Pipelinez.AzureServiceBus.Client;

internal interface IAzureServiceBusClientFactory
{
    IAzureServiceBusSender CreateSender(AzureServiceBusDestinationOptions options);

    IAzureServiceBusSender CreateDeadLetterSender(AzureServiceBusDeadLetterOptions options);

    IAzureServiceBusProcessor CreateProcessor(AzureServiceBusSourceOptions options);
}
