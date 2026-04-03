# Pipelinez

Typed data pipelines for .NET.

Pipelinez is a small .NET 8 framework for building record-processing pipelines with a consistent runtime model:

- strongly typed records
- pluggable sources, segments, and destinations
- async startup and completion
- fault tracking and configurable error policies
- transport extensions such as Kafka

## How It Works

A pipeline is built from three concepts:

- `Source`
  where records come from
- `Segment`
  one or more transformation steps
- `Destination`
  where processed records end up

At runtime, records flow like this:

`source -> segment -> segment -> destination`

Each record is wrapped in a `PipelineContainer<T>` so the framework can carry:

- the record payload
- metadata
- fault state
- segment execution history

## Quick Example

```csharp
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
    public required decimal Total { get; set; }
}

public sealed class ApplyDiscountSegment : PipelineSegment<OrderRecord>
{
    public override Task<OrderRecord> ExecuteAsync(OrderRecord record)
    {
        record.Total *= 0.9m;
        return Task.FromResult(record);
    }
}

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .AddSegment(new ApplyDiscountSegment(), new object())
    .WithInMemoryDestination("in-memory")
    .Build();

pipeline.OnPipelineRecordCompleted += (_, args) =>
{
    Console.WriteLine($"{args.Record.Id}: {args.Record.Total}");
};

await pipeline.StartPipelineAsync();
await pipeline.PublishAsync(new OrderRecord { Id = "A-100", Total = 42m });
await pipeline.CompleteAsync();
await pipeline.Completion;
```

## Kafka Support

Kafka support lives in the separate `Pipelinez.Kafka` assembly and extends the core builder with:

- `WithKafkaSource(...)`
- `WithKafkaDestination(...)`

Example shape:

```csharp
using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Configuration;

var pipeline = Pipeline<MyRecord>.New("kafka-pipeline")
    .WithKafkaSource(
        new KafkaSourceOptions
        {
            BootstrapServers = "localhost:9092",
            TopicName = "input-topic",
            ConsumerGroup = "pipelinez-demo",
            StartOffsetFromBeginning = true,
            SecurityProtocol = SecurityProtocol.Plaintext
        },
        (string key, string value) => new MyRecord { Key = key, Value = value })
    .WithKafkaDestination(
        new KafkaDestinationOptions
        {
            BootstrapServers = "localhost:9092",
            TopicName = "output-topic",
            SecurityProtocol = SecurityProtocol.Plaintext
        },
        (MyRecord record) => new Message<string, string>
        {
            Key = record.Key,
            Value = record.Value
        })
    .Build();
```

The repo also includes:

- a runnable Kafka example in [`src/examples/Example.Kafka`](src/examples/Example.Kafka)
- a Kafka data generator in [`src/examples/Example.Kafka.DataGen`](src/examples/Example.Kafka.DataGen)
- Docker-backed Kafka integration tests in [`src/tests/Pipelinez.Kafka.Tests`](src/tests/Pipelinez.Kafka.Tests)

## Error Handling

Pipelinez supports explicit fault handling through `WithErrorHandler(...)`.

Available actions:

- `SkipRecord`
  continue processing later records
- `StopPipeline`
  fault and stop the pipeline
- `Rethrow`
  fault the pipeline and surface the original exception

Public events include:

- `OnPipelineRecordCompleted`
- `OnPipelineRecordFaulted`
- `OnPipelineFaulted`

## Project Layout

- [`src/Pipelinez`](src/Pipelinez)
  core runtime
- [`src/Pipelinez.Kafka`](src/Pipelinez.Kafka)
  Kafka transport extension
- [`src/tests/Pipelinez.Tests`](src/tests/Pipelinez.Tests)
  core tests
- [`src/tests/Pipelinez.Kafka.Tests`](src/tests/Pipelinez.Kafka.Tests)
  Kafka integration tests
- [`docs/Overview.md`](docs/Overview.md)
  deeper architectural overview

## Running Locally

Build the solution:

```bash
dotnet build src/Pipelinez.sln
```

Run the full test suite:

```bash
dotnet test src/Pipelinez.sln
```

Run the Kafka example:

```bash
dotnet run --project src/examples/Example.Kafka
```

Run the Kafka data generator:

```bash
dotnet run --project src/examples/Example.Kafka.DataGen
```

The Kafka examples and Kafka integration tests use Docker/Testcontainers for local broker startup unless you provide an existing broker through environment variables.

## Status

Current implemented capabilities include:

- async pipeline lifecycle
- fault-aware record containers
- segment execution history
- configurable error policies
- async destination execution
- Kafka source and destination support
- Docker-backed Kafka integration coverage

## License

No license file is currently included in the repository.
