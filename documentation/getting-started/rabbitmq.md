# Getting Started: RabbitMQ Pipeline

This guide creates a Pipelinez pipeline that consumes RabbitMQ messages from one queue and publishes processed messages to another queue.

## Install

```bash
dotnet add package Pipelinez.RabbitMQ
```

## Minimal Pipeline

```csharp
using System.Text;
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.RabbitMQ;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Destination;

var connection = new RabbitMqConnectionOptions
{
    Uri = new Uri("amqp://guest:guest@localhost:5672/")
};

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithRabbitMqSource(
        new RabbitMqSourceOptions
        {
            Connection = connection,
            Queue = RabbitMqQueueOptions.Named("orders-in"),
            PrefetchCount = 4
        },
        delivery => new OrderRecord
        {
            Id = delivery.Properties?.MessageId ?? delivery.DeliveryTag.ToString(),
            Payload = Encoding.UTF8.GetString(delivery.Body.Span)
        })
    .WithRabbitMqDestination(
        new RabbitMqDestinationOptions
        {
            Connection = connection,
            RoutingKey = "orders-out"
        },
        record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(record.Payload)))
    .Build();

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
    public required string Payload { get; init; }
}
```

`RoutingKey = "orders-out"` with an empty exchange publishes through RabbitMQ's default exchange directly to the `orders-out` queue.

## Local Broker

Use Docker for a local RabbitMQ broker:

```bash
docker run --rm -p 5672:5672 -p 15672:15672 rabbitmq:3.13-management
```

The management UI is available at `http://localhost:15672` with user name `guest` and password `guest`.

## Dead-Letter Behavior

Pipelinez dead-letter destinations and RabbitMQ native dead-letter exchanges are separate:

- `WithRabbitMqDeadLetterDestination(...)` publishes a Pipelinez dead-letter envelope.
- `RabbitMqPipelineDeadLetterSettlement.DeadLetterOrDiscardSourceMessage` nacks the original source delivery with `requeue = false`.

Use both only when you intentionally want both a Pipelinez dead-letter message and native RabbitMQ DLX routing.
