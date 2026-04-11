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

## When To Use This Package

Use this package when Kafka is part of your record-processing pipeline and you want Pipelinez to manage the pipeline lifecycle, retries, dead-lettering, backpressure, worker ownership, and partition-aware execution around Kafka records.

## Install

```bash
dotnet add package Pipelinez.Kafka
```

`Pipelinez.Kafka` depends on `Pipelinez`, so you do not need to add both explicitly unless you prefer to do so.

Related transport package in this repository:

- `Pipelinez`
  core pipeline runtime
- `Pipelinez.PostgreSql`
  PostgreSQL destination and dead-letter transport extensions

## Quick Example

```csharp
using Confluent.Kafka;
using Pipelinez.Core;
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
```

## More Information

- NuGet: https://www.nuget.org/packages/Pipelinez.Kafka
- Repository: https://github.com/KenBerg75/Pipelinez
- Kafka docs: https://github.com/KenBerg75/Pipelinez/blob/main/docs/transports/kafka.md
