# Pipelinez.RabbitMQ

RabbitMQ transport extensions for Pipelinez.

Use `Pipelinez.RabbitMQ` when a Pipelinez pipeline needs to read from RabbitMQ queues, publish to exchanges or queues, dead-letter failed records to RabbitMQ, or run competing-consumer workers.

## What This Package Does

`Pipelinez.RabbitMQ` adds:

- `WithRabbitMqSource(...)`
- `WithRabbitMqDestination(...)`
- `WithRabbitMqDeadLetterDestination(...)`
- RabbitMQ configuration types
- manual-ack queue source support
- exchange and routing-key destination support
- Pipelinez dead-letter publishing to RabbitMQ
- competing-consumer distributed execution support

## Install

```bash
dotnet add package Pipelinez.RabbitMQ
```

`Pipelinez.RabbitMQ` depends on `Pipelinez`, so you do not need to add both explicitly unless you prefer to do so.

## Minimal Example

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
            Queue = RabbitMqQueueOptions.Named("orders-in")
        },
        delivery => new OrderRecord
        {
            Id = delivery.Properties?.MessageId ?? delivery.DeliveryTag.ToString(),
            Body = Encoding.UTF8.GetString(delivery.Body.Span)
        })
    .WithRabbitMqDestination(
        new RabbitMqDestinationOptions
        {
            Connection = connection,
            Exchange = "orders",
            RoutingKey = "processed"
        },
        record => RabbitMqPublishMessage.Create(
            Encoding.UTF8.GetBytes(record.Body),
            routingKey: "processed"))
    .Build();

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
    public required string Body { get; init; }
}
```

## Documentation

- NuGet: https://www.nuget.org/packages/Pipelinez.RabbitMQ
- Repository: https://github.com/KenBerg75/Pipelinez
- API reference: https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html
