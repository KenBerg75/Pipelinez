# RabbitMQ

Audience: application developers and operators using the RabbitMQ transport extension.

## What This Covers

- queue sources
- exchange and routing-key destinations
- default-exchange queue publishing
- Pipelinez dead-letter publishing
- source delivery settlement
- competing-consumer distributed execution
- local Docker/Testcontainers usage

## Current Transport Status

RabbitMQ support lives in:

- `src/Pipelinez.RabbitMQ`

The transport package is:

- `Pipelinez.RabbitMQ`

## Basic Configuration

```csharp
using Pipelinez.RabbitMQ.Configuration;

var connection = new RabbitMqConnectionOptions
{
    Uri = new Uri("amqp://guest:guest@localhost:5672/")
};

var sourceOptions = new RabbitMqSourceOptions
{
    Connection = connection,
    Queue = RabbitMqQueueOptions.Named("orders-in"),
    PrefetchCount = 8
};

var destinationOptions = new RabbitMqDestinationOptions
{
    Connection = connection,
    Exchange = "orders",
    RoutingKey = "processed"
};
```

For host-based configuration, use `HostName`, `Port`, `VirtualHost`, `UserName`, and `Password`.

## Builder Example

```csharp
using System.Text;
using Pipelinez.Core;
using Pipelinez.RabbitMQ;
using Pipelinez.RabbitMQ.Destination;

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithRabbitMqSource(
        sourceOptions,
        delivery => new OrderRecord
        {
            Id = delivery.Properties?.MessageId ?? delivery.DeliveryTag.ToString(),
            Payload = Encoding.UTF8.GetString(delivery.Body.Span)
        })
    .WithRabbitMqDestination(
        destinationOptions,
        record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(record.Payload)))
    .Build();
```

## Default Exchange Destination

To publish directly to a queue, leave `Exchange` empty and set `RoutingKey` to the queue name.

```csharp
var destinationOptions = new RabbitMqDestinationOptions
{
    Connection = connection,
    RoutingKey = "orders-out"
};
```

## Dead-Letter Destination

Pipelinez dead-lettering writes a `PipelineDeadLetterRecord<T>` to an exchange and routing key that you choose.

```csharp
var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithRabbitMqSource(sourceOptions, delivery => new OrderRecord
    {
        Id = delivery.Properties?.MessageId ?? delivery.DeliveryTag.ToString(),
        Payload = Encoding.UTF8.GetString(delivery.Body.Span)
    })
    .WithRabbitMqDestination(destinationOptions, record =>
        RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(record.Payload)))
    .WithRabbitMqDeadLetterDestination(
        new RabbitMqDeadLetterOptions
        {
            Connection = connection,
            Exchange = "orders.dead",
            RoutingKey = "failed"
        },
        deadLetter => RabbitMqPublishMessage.Create(
            Encoding.UTF8.GetBytes(deadLetter.Record.Payload)))
    .Build();
```

## Source Settlement

Pipelinez uses manual acknowledgements.

The source delivery is settled only after Pipelinez reaches a terminal outcome:

- successful destination write: `basic.ack`
- `PipelineErrorAction.SkipRecord`: `basic.ack`
- `PipelineErrorAction.DeadLetter`: `basic.ack` by default
- `PipelineErrorAction.StopPipeline`: `basic.nack` with requeue by default
- `PipelineErrorAction.Rethrow`: `basic.nack` with requeue by default

You can opt into native RabbitMQ dead-letter exchange routing for Pipelinez dead-letter actions:

```csharp
var sourceOptions = new RabbitMqSourceOptions
{
    Connection = connection,
    Queue = RabbitMqQueueOptions.Named("orders-in"),
    Settlement = new RabbitMqSourceSettlementOptions
    {
        PipelineDeadLetterSettlement =
            RabbitMqPipelineDeadLetterSettlement.DeadLetterOrDiscardSourceMessage
    }
};
```

That uses `basic.nack` with `requeue = false`. RabbitMQ only routes the message to a dead-letter exchange if the queue has DLX behavior configured. Without DLX configuration, RabbitMQ discards the message.

## Topology

Pipelinez does not declare or mutate topology by default.

Use `RabbitMqTopologyOptions` when local examples or controlled applications should declare queues, exchanges, and bindings. For production, broker policies are usually the safer place to manage dead-letter exchange settings.

## Metadata And Headers

The source copies RabbitMQ headers into `PipelineRecord.Headers` when they can be represented as strings.

It also stamps metadata keys such as:

- `pipelinez.rabbitmq.queue_name`
- `pipelinez.rabbitmq.exchange`
- `pipelinez.rabbitmq.routing_key`
- `pipelinez.rabbitmq.consumer_tag`
- `pipelinez.rabbitmq.delivery_tag`
- `pipelinez.rabbitmq.redelivered`
- `pipelinez.rabbitmq.message_id`
- `pipelinez.rabbitmq.correlation_id`

Destinations copy `PipelineRecord.Headers` into RabbitMQ headers without overwriting headers already set by the message mapper.

## Publisher Confirms And Mandatory Publishes

Destinations enable publisher confirms and mandatory publishing by default.

That means a Pipelinez destination marks a record complete only after RabbitMQ accepts the publish, and unroutable mandatory messages fail instead of silently disappearing.

## Distributed Execution

RabbitMQ uses competing consumers for queues.

The Pipelinez source supports `PipelineExecutionMode.Distributed` and reports a logical lease for the queue. That lease is observational: RabbitMQ owns the actual message distribution.

## Related Docs

- [Getting Started: RabbitMQ](../getting-started/rabbitmq.md)
- [Architecture: RabbitMQ](../architecture/rabbitmq.md)
- [Distributed Execution](../guides/distributed-execution.md)
- [Dead-Lettering](../guides/dead-lettering.md)
