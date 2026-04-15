# Pipelinez.AzureServiceBus

Azure Service Bus transport extensions for Pipelinez.

Use `Pipelinez.AzureServiceBus` when a Pipelinez pipeline needs to read from Azure Service Bus queues or topic subscriptions, write to queues or topics, dead-letter failed records to Service Bus, or run competing-consumer workers.

## What This Package Does

`Pipelinez.AzureServiceBus` adds:

- `WithAzureServiceBusSource(...)`
- `WithAzureServiceBusDestination(...)`
- `WithAzureServiceBusDeadLetterDestination(...)`
- Azure Service Bus configuration types
- queue and topic subscription source support
- queue and topic destination support
- Pipelinez dead-letter publishing to Service Bus
- competing-consumer distributed execution support

## Install

```bash
dotnet add package Pipelinez.AzureServiceBus
```

`Pipelinez.AzureServiceBus` depends on `Pipelinez`, so you do not need to add both explicitly unless you prefer to do so.

## Minimal Example

```csharp
using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.Core;
using Pipelinez.Core.Record;

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithAzureServiceBusSource(
        new AzureServiceBusSourceOptions
        {
            Connection = new AzureServiceBusConnectionOptions
            {
                ConnectionString = "Endpoint=sb://..."
            },
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-in")
        },
        message => new OrderRecord
        {
            Id = message.MessageId,
            Body = message.Body.ToString()
        })
    .WithAzureServiceBusDestination(
        new AzureServiceBusDestinationOptions
        {
            Connection = new AzureServiceBusConnectionOptions
            {
                ConnectionString = "Endpoint=sb://..."
            },
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-out")
        },
        record => new ServiceBusMessage(BinaryData.FromString(record.Body))
        {
            MessageId = record.Id
        })
    .Build();

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
    public required string Body { get; init; }
}
```

## Documentation

- NuGet: https://www.nuget.org/packages/Pipelinez.AzureServiceBus
- Repository: https://github.com/KenBerg75/Pipelinez
- API reference: https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html
