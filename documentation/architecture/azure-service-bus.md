# Azure Service Bus Internals

Audience: contributors and maintainers working on the Azure Service Bus transport.

## Source Responsibilities

`AzureServiceBusPipelineSource<T>` is responsible for:

- creating a `ServiceBusProcessor`
- consuming from queues or topic subscriptions
- mapping `ServiceBusReceivedMessage` values into pipeline records
- copying application properties into record headers
- stamping Service Bus and distributed metadata
- waiting for terminal Pipelinez handling before settling source messages
- reporting a logical distributed lease for competing-consumer workers

## Settlement Model

The source intentionally separates:

- receive-time message lock acquisition
- Pipelinez publish/admission
- segment and destination execution
- terminal Pipelinez fault handling
- Service Bus settlement

The source uses `AutoCompleteMessages = false` and `ServiceBusReceiveMode.PeekLock`.

The processor callback waits for the corresponding Pipelinez container to complete or fault-handled. That keeps SDK lock renewal aligned with actual processing instead of merely handing a record to the dataflow graph.

## Terminal Actions

Settlement defaults:

- completed container -> `CompleteMessageAsync`
- `SkipRecord` -> `CompleteMessageAsync`
- `DeadLetter` -> `CompleteMessageAsync`
- `StopPipeline` -> `AbandonMessageAsync`
- `Rethrow` -> `AbandonMessageAsync`

`AzureServiceBusSourceSettlementOptions` can move `DeadLetter` outcomes to the native Service Bus DLQ instead.

## Destination Responsibilities

`AzureServiceBusPipelineDestination<T>` is responsible for:

- creating a `ServiceBusSender`
- mapping a pipeline record into a `ServiceBusMessage`
- copying pipeline headers into application properties
- preserving mapper-provided application properties
- awaiting `SendMessageAsync`

The record is only considered completed after Service Bus accepts the send.

## Dead-Letter Destination

`AzureServiceBusDeadLetterDestination<T>` writes a Pipelinez dead-letter envelope to a queue or topic selected by the consumer.

It adds fault metadata such as:

- fault component
- component kind
- fault timestamp
- dead-letter timestamp
- source transport and lease data

This is distinct from native Service Bus DLQ settlement.

## Distributed Execution

For non-session entities, Service Bus distributes messages across competing consumers. Pipelinez reports one logical lease for the configured queue or topic subscription so status and worker events remain useful.

That logical lease is not exclusive ownership. Session-aware exclusive ownership can be added later with `ServiceBusSessionProcessor`.

## Related Docs

- [Azure Service Bus](../transports/azure-service-bus.md)
- [Distributed Execution](../guides/distributed-execution.md)
- [Architecture: Runtime](runtime.md)
