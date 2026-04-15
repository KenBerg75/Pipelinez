# Azure Service Bus

Audience: application developers and operators using the Azure Service Bus transport extension.

## What This Covers

- queue and topic subscription sources
- queue and topic destinations
- Pipelinez dead-letter publishing
- source message settlement
- competing-consumer distributed execution
- local emulator and cloud namespace usage

## Current Transport Status

Azure Service Bus support lives in:

- `src/Pipelinez.AzureServiceBus`

The transport package is:

- `Pipelinez.AzureServiceBus`

## Basic Configuration

```csharp
using Pipelinez.AzureServiceBus.Configuration;

var sourceOptions = new AzureServiceBusSourceOptions
{
    Connection = new AzureServiceBusConnectionOptions
    {
        ConnectionString = "Endpoint=sb://..."
    },
    Entity = AzureServiceBusEntityOptions.ForQueue("orders-in"),
    MaxConcurrentCalls = 4,
    MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
};

var destinationOptions = new AzureServiceBusDestinationOptions
{
    Connection = new AzureServiceBusConnectionOptions
    {
        ConnectionString = "Endpoint=sb://..."
    },
    Entity = AzureServiceBusEntityOptions.ForQueue("orders-out")
};
```

For managed identity or another Azure credential, use `FullyQualifiedNamespace` and a `TokenCredential`.

```csharp
using Azure.Identity;

var connection = new AzureServiceBusConnectionOptions
{
    FullyQualifiedNamespace = "contoso.servicebus.windows.net",
    Credential = new DefaultAzureCredential()
};
```

## Builder Example

```csharp
using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus;
using Pipelinez.Core;

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithAzureServiceBusSource(
        sourceOptions,
        message => new OrderRecord
        {
            Id = message.MessageId,
            Payload = message.Body.ToString()
        })
    .WithAzureServiceBusDestination(
        destinationOptions,
        record => new ServiceBusMessage(BinaryData.FromString(record.Payload))
        {
            MessageId = record.Id
        })
    .Build();
```

## Topic Subscription Source

Use a topic subscription when records are published to a topic but this pipeline owns one subscription.

```csharp
var sourceOptions = new AzureServiceBusSourceOptions
{
    Connection = connection,
    Entity = AzureServiceBusEntityOptions.ForTopicSubscription("orders", "pipelinez-workers")
};
```

Bare topics are destinations only. A source must use either a queue or a topic subscription.

## Dead-Letter Destination

Pipelinez dead-lettering writes a `PipelineDeadLetterRecord<T>` to a queue or topic that you choose.

```csharp
var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithAzureServiceBusSource(sourceOptions, message => new OrderRecord
    {
        Id = message.MessageId,
        Payload = message.Body.ToString()
    })
    .WithAzureServiceBusDestination(destinationOptions, record =>
        new ServiceBusMessage(BinaryData.FromString(record.Payload)))
    .WithAzureServiceBusDeadLetterDestination(
        new AzureServiceBusDeadLetterOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-dead-letter")
        },
        deadLetter => new ServiceBusMessage(BinaryData.FromString(deadLetter.Record.Payload))
        {
            MessageId = deadLetter.Record.Id
        })
    .Build();
```

## Source Settlement

Pipelinez uses `PeekLock` receive mode and disables Azure SDK auto-completion.

The source message is settled only after Pipelinez reaches a terminal outcome:

- successful destination write: complete the source message
- `PipelineErrorAction.SkipRecord`: complete the source message
- `PipelineErrorAction.DeadLetter`: complete the source message by default
- `PipelineErrorAction.StopPipeline`: abandon the source message by default
- `PipelineErrorAction.Rethrow`: abandon the source message by default

You can opt into native Service Bus dead-letter settlement for Pipelinez dead-letter actions:

```csharp
var sourceOptions = new AzureServiceBusSourceOptions
{
    Connection = connection,
    Entity = AzureServiceBusEntityOptions.ForQueue("orders-in"),
    Settlement = new AzureServiceBusSourceSettlementOptions
    {
        PipelineDeadLetterSettlement =
            AzureServiceBusPipelineDeadLetterSettlement.DeadLetterSourceMessage
    }
};
```

That native DLQ behavior is separate from `WithAzureServiceBusDeadLetterDestination(...)`. If both are configured, the original message is moved to the native Service Bus DLQ and the Pipelinez dead-letter envelope is published to the configured dead-letter entity.

## Metadata And Headers

The source copies Service Bus application properties into `PipelineRecord.Headers` when they can be represented as strings.

It also stamps metadata keys such as:

- `pipelinez.asb.message_id`
- `pipelinez.asb.sequence_number`
- `pipelinez.asb.delivery_count`
- `pipelinez.asb.lock_token`
- `pipelinez.asb.enqueued_time_utc`
- `pipelinez.asb.session_id`
- `pipelinez.asb.correlation_id`

Destinations copy `PipelineRecord.Headers` into `ServiceBusMessage.ApplicationProperties` without overwriting properties already set by the message mapper.

## Distributed Execution

Azure Service Bus uses competing consumers for non-session queues and subscriptions.

The Pipelinez source supports `PipelineExecutionMode.Distributed` and reports a logical lease for the queue or topic subscription. That lease is observational: Service Bus owns the actual message distribution.

Session-aware exclusive ownership is not part of the first implementation.

## Local Development

Use a real Azure Service Bus namespace or the official Azure Service Bus emulator.

The emulator runs in Docker and uses a development connection string with `UseDevelopmentEmulator=true`. Set this for the example app:

```powershell
$env:PIPELINEZ_EXAMPLE_ASB_CONNECTION_STRING='Endpoint=sb://127.0.0.1;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;'
dotnet run --project src/examples/Example.AzureServiceBus
```

## Related Docs

- [Getting Started: Azure Service Bus](../getting-started/azure-service-bus.md)
- [Architecture: Azure Service Bus](../architecture/azure-service-bus.md)
- [Distributed Execution](../guides/distributed-execution.md)
- [Dead-Lettering](../guides/dead-lettering.md)
