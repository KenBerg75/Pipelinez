# Kafka Pipeline

Audience: application developers who want a real transport-backed pipeline.

## What This Covers

This guide shows the shape of a Kafka-backed pipeline and how to run the shipped examples with a local Docker-hosted broker.

## Prerequisites

- .NET 8 SDK
- Docker running locally
- either:
  - a consumer project referencing the `Pipelinez.Kafka` package when published
  - or a local clone of this repository

Expected package install command:

```bash
dotnet add package Pipelinez.Kafka
```

## Fastest Path: Run The Example

From the repository root:

```bash
dotnet run --project src/examples/Example.Kafka
```

This example:

- starts or connects to Kafka
- ensures the example topics exist
- builds a Kafka source -> segment -> Kafka destination pipeline
- publishes demo records into the source topic
- waits for the pipeline to process them

To publish additional traffic into the same source topic:

```bash
dotnet run --project src/examples/Example.Kafka.DataGen
```

## Minimal Kafka Pipeline Shape

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
            ConsumerGroup = "orders-demo",
            StartOffsetFromBeginning = true,
            SecurityProtocol = SecurityProtocol.Plaintext
        },
        (string key, string value) => new MyRecord
        {
            Key = key,
            Value = value
        })
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

## Local Example Environment Variables

The example apps support a small environment-variable surface:

- `PIPELINEZ_EXAMPLE_BOOTSTRAP_SERVERS`
- `PIPELINEZ_EXAMPLE_KAFKA_IMAGE`
- `PIPELINEZ_EXAMPLE_KAFKA_STARTUP_TIMEOUT_SECONDS`
- `PIPELINEZ_EXAMPLE_KAFKA_REUSE_CONTAINER`
- `PIPELINEZ_EXAMPLE_SOURCE_TOPIC`
- `PIPELINEZ_EXAMPLE_DESTINATION_TOPIC`
- `PIPELINEZ_EXAMPLE_CONSUMER_GROUP`
- `PIPELINEZ_EXAMPLE_MESSAGE_COUNT`

If `PIPELINEZ_EXAMPLE_BOOTSTRAP_SERVERS` is not set, the examples start a local Kafka container automatically.

## Important Behaviors

- Kafka support lives in the separate `Pipelinez.Kafka` assembly.
- `StartOffsetFromBeginning` affects new consumer groups without committed offsets.
- committed offsets are resumed normally when they already exist.
- distributed Kafka execution is available when you opt into `PipelineExecutionMode.Distributed`.

## Next Steps

- read [Kafka Transport](../transports/kafka.md)
- read [Distributed Execution](../guides/distributed-execution.md)
- read [Troubleshooting](../operations/troubleshooting.md)
