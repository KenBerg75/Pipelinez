# Kafka

Audience: application developers and operators using the Kafka transport extension.

## What This Covers

- Kafka source and destination setup
- local Docker-backed development
- dead-lettering
- distributed execution
- partition-aware scaling

## Current Transport Status

Kafka is the only transport extension currently implemented in this repository.

Kafka support lives in:

- `src/Pipelinez.Kafka`

## Basic Configuration

```csharp
using Confluent.Kafka;
using Pipelinez.Kafka.Configuration;

var sourceOptions = new KafkaSourceOptions
{
    BootstrapServers = "localhost:9092",
    TopicName = "orders-in",
    ConsumerGroup = "orders-workers",
    StartOffsetFromBeginning = true,
    SecurityProtocol = SecurityProtocol.Plaintext
};

var destinationOptions = new KafkaDestinationOptions
{
    BootstrapServers = "localhost:9092",
    TopicName = "orders-out",
    SecurityProtocol = SecurityProtocol.Plaintext
};
```

## Builder Example

```csharp
var pipeline = Pipeline<MyRecord>.New("orders")
    .WithKafkaSource(
        sourceOptions,
        (string key, string value) => new MyRecord { Key = key, Value = value })
    .WithKafkaDestination(
        destinationOptions,
        record => new Message<string, string>
        {
            Key = record.Key,
            Value = record.Value
        })
    .Build();
```

## Dead-Letter Topic Example

```csharp
var pipeline = Pipeline<MyRecord>.New("orders")
    .WithKafkaSource(sourceOptions, (string key, string value) => new MyRecord { Key = key, Value = value })
    .WithKafkaDestination(destinationOptions, record => new Message<string, string> { Key = record.Key, Value = record.Value })
    .WithKafkaDeadLetterDestination(
        new KafkaDestinationOptions
        {
            BootstrapServers = "localhost:9092",
            TopicName = "orders-dead-letter",
            SecurityProtocol = SecurityProtocol.Plaintext
        },
        deadLetter => new Message<string, string>
        {
            Key = deadLetter.Record.Key,
            Value = deadLetter.Record.Value
        })
    .Build();
```

## Partition-Aware Scaling

```csharp
var sourceOptions = new KafkaSourceOptions
{
    BootstrapServers = "localhost:9092",
    TopicName = "orders-in",
    ConsumerGroup = "orders-workers",
    SecurityProtocol = SecurityProtocol.Plaintext,
    PartitionScaling = new KafkaPartitionScalingOptions
    {
        ExecutionMode = KafkaPartitionExecutionMode.ParallelizeAcrossPartitions,
        MaxConcurrentPartitions = 4,
        MaxInFlightPerPartition = 1,
        RebalanceMode = KafkaPartitionRebalanceMode.DrainAndYield
    }
};
```

Important rules:

- `PreservePartitionOrder` keeps `MaxInFlightPerPartition` at `1`
- `RelaxOrderingWithinPartition` is the explicit opt-in mode for more than one in-flight record per partition

## Local Development

The repository examples use Docker/Testcontainers for local Kafka startup.

Fastest path:

```bash
dotnet run --project src/examples/Example.Kafka
```

If you want to connect to an existing broker instead of starting a container, set:

- `PIPELINEZ_EXAMPLE_BOOTSTRAP_SERVERS`

## Offset Behavior

- committed offsets are resumed when they already exist
- `StartOffsetFromBeginning` affects new consumer groups without stored offsets
- completion drives explicit offset storage rather than immediate consume-time storage

## Related Docs

- [Getting Started: Kafka Pipeline](../getting-started/kafka.md)
- [Distributed Execution](../guides/distributed-execution.md)
- [Troubleshooting](../operations/troubleshooting.md)
