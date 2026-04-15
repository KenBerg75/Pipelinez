# Azure Service Bus Pipeline

Audience: application developers who want to consume from or publish to Azure Service Bus.

## Install

```bash
dotnet add package Pipelinez.AzureServiceBus
```

## Minimal Queue Pipeline

```csharp
using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.Core;
using Pipelinez.Core.Record;

var connection = new AzureServiceBusConnectionOptions
{
    ConnectionString = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING")
};

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithAzureServiceBusSource(
        new AzureServiceBusSourceOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-in")
        },
        message => new OrderRecord
        {
            Id = message.MessageId,
            Payload = message.Body.ToString()
        })
    .WithAzureServiceBusDestination(
        new AzureServiceBusDestinationOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-out")
        },
        record => new ServiceBusMessage(BinaryData.FromString(record.Payload))
        {
            MessageId = record.Id
        })
    .Build();

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
    public required string Payload { get; init; }
}
```

## Run The Example

Set a connection string for a real namespace or local emulator:

```powershell
$env:PIPELINEZ_EXAMPLE_ASB_CONNECTION_STRING='Endpoint=sb://...'
dotnet run --project src/examples/Example.AzureServiceBus
```

The example creates input, output, and dead-letter queues when the connection has management rights.

## Important Behavior

- the source uses `PeekLock`
- SDK auto-completion is disabled
- source messages complete only after the Pipelinez destination succeeds or terminal fault handling completes
- Pipelinez dead-letter publishing is separate from the native Service Bus DLQ
- non-session distributed execution uses Service Bus competing consumers

## Next Steps

- read [Azure Service Bus Transport](../transports/azure-service-bus.md)
- read [Architecture: Azure Service Bus](../architecture/azure-service-bus.md)
- read [Error Handling](../guides/error-handling.md)
- read [Dead-Lettering](../guides/dead-lettering.md)
