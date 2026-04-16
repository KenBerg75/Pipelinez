# Pipelinez.Kafka

Kafka transport extensions for Pipelinez.

Use `Pipelinez.Kafka` when a Pipelinez pipeline needs to read from Kafka topics, write to Kafka topics, dead-letter failed records to Kafka, or run partition-aware distributed Kafka workers.

## What This Package Does

`Pipelinez.Kafka` adds:

- `WithKafkaSource(...)`
- `WithKafkaDestination(...)`
- `WithKafkaDeadLetterDestination(...)`
- Kafka configuration types
- Kafka-backed distributed execution support
- partition-aware scaling controls

## Install

```bash
dotnet add package Pipelinez.Kafka
```

`Pipelinez.Kafka` depends on `Pipelinez`, so you do not need to add both explicitly unless you prefer to do so.

## When To Use This Package

Use this package when Kafka is part of your record-processing pipeline and you want Pipelinez to manage the pipeline lifecycle, retries, dead-lettering, backpressure, worker ownership, and partition-aware execution around Kafka records.

## Minimal Example

```csharp
using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Configuration;

var pipeline = Pipeline<MyRecord>.New("orders")
    .WithKafkaSource(
        new KafkaSourceOptions
        {
            BootstrapServers = "localhost:9092",
            TopicName = "orders-in",
            ConsumerGroup = "orders-workers",
            SecurityProtocol = SecurityProtocol.Plaintext
        },
        (string key, string value) => new MyRecord { Key = key, Value = value })
    .WithKafkaDestination(
        new KafkaDestinationOptions
        {
            BootstrapServers = "localhost:9092",
            TopicName = "orders-out",
            SecurityProtocol = SecurityProtocol.Plaintext
        },
        record => new Message<string, string>
        {
            Key = record.Key,
            Value = record.Value
        })
    .Build();

public sealed class MyRecord : PipelineRecord
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}
```

## Common Recipes

- Read records from one Kafka topic and publish transformed records to another topic.
- Dead-letter failed Kafka records to a dedicated Kafka topic.
- Run partition-aware distributed workers with explicit worker identity.
- Combine `Pipelinez.Kafka` with `Pipelinez.PostgreSql` to write processed Kafka records to PostgreSQL.

## Related Packages

- [`Pipelinez`](https://www.nuget.org/packages/Pipelinez)
  core pipeline runtime.
- [`Pipelinez.PostgreSql`](https://www.nuget.org/packages/Pipelinez.PostgreSql)
  PostgreSQL destination and dead-letter writes.
- [`Pipelinez.RabbitMQ`](https://www.nuget.org/packages/Pipelinez.RabbitMQ)
  RabbitMQ source, destination, dead-lettering, and competing-consumer workers.

## Documentation

- NuGet: https://www.nuget.org/packages/Pipelinez.Kafka
- Repository: https://github.com/KenBerg75/Pipelinez
- API reference: https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html
- Getting started: https://github.com/KenBerg75/Pipelinez/blob/main/documentation/getting-started/kafka.md
- Kafka docs: https://github.com/KenBerg75/Pipelinez/blob/main/documentation/transports/kafka.md
