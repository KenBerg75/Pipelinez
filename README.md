# Pipelinez

Typed, observable record-processing pipelines for .NET.

Pipelinez is a .NET 8 library for building typed record-processing pipelines inside normal application code. It gives you a consistent way to move records from a source, through one or more processing steps, into a destination, with built-in lifecycle management, retries, dead-lettering, flow control, and runtime observability.

If your application needs to ingest, enrich, transform, route, or publish records without hand-wiring the surrounding runtime behavior each time, Pipelinez is the layer that organizes that work.

## What Pipelinez Is

Pipelinez lets you define a pipeline as:

- a `Source` that produces records
- one or more `Segment`s that transform or enrich them
- a `Destination` that writes the final result

At runtime, Pipelinez manages the lifecycle, fault handling, retries, backpressure, diagnostics, and transport integration around that flow.

## When To Use It

Pipelinez is a good fit when you want to:

- consume records from memory, Kafka, or another transport
- apply one or more typed processing steps
- route successful and failed records intentionally
- observe runtime behavior with status, health, events, and metrics
- keep pipeline logic testable without wiring TPL Dataflow and transport details by hand

Pipelinez is not trying to replace large distributed stream-processing platforms. It is a focused .NET library for applications that want a typed, testable, observable pipeline runtime inside normal application code.

## Installation

Package generation is configured for:

- `Pipelinez`
- `Pipelinez.Kafka`

Expected install commands for published packages:

```bash
dotnet add package Pipelinez
```

For Kafka support:

```bash
dotnet add package Pipelinez.Kafka
```

`Pipelinez.Kafka` depends on `Pipelinez`, so Kafka consumers do not need to add both explicitly unless they want to.

Public package publishing is configured through GitHub tag releases and NuGet Trusted Publishing. Until the first public release is available on NuGet.org, you can still build and pack the projects locally from this repository.

## Quick Example

This example creates an in-memory pipeline that receives an `OrderRecord`, applies a discount, and completes through an in-memory destination.

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

## How It Works

At a high level, records flow like this:

`source -> segment -> segment -> destination`

Each record is wrapped in a `PipelineContainer<T>` so the runtime can carry:

- the record payload
- metadata
- fault state
- segment execution history
- retry history

That container model is what allows Pipelinez to keep runtime behavior explicit without making every segment or destination manage its own bookkeeping.

## Key Capabilities

- typed records and pluggable sources, segments, and destinations
- async startup, publish, completion, and shutdown behavior
- configurable retry, error-handling, and dead-letter flows
- explicit flow control, saturation visibility, and publish results
- performance tuning, batching, and runtime performance snapshots
- distributed execution and partition-aware Kafka scaling
- health checks, metrics, correlation IDs, and runtime events

## Kafka Support

Kafka support lives in the separate `Pipelinez.Kafka` package and adds:

- Kafka source support
- Kafka destination support
- Kafka dead-letter destinations
- distributed worker coordination
- partition-aware scaling

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

For a full walkthrough, see:

- [`docs/getting-started/kafka.md`](docs/getting-started/kafka.md)
- [`src/examples/Example.Kafka`](src/examples/Example.Kafka)
- [`src/examples/Example.Kafka.DataGen`](src/examples/Example.Kafka.DataGen)
- [`src/tests/Pipelinez.Kafka.Tests`](src/tests/Pipelinez.Kafka.Tests)

## Learn More

- New to Pipelinez: [`docs/getting-started/in-memory.md`](docs/getting-started/in-memory.md)
- Using Kafka: [`docs/getting-started/kafka.md`](docs/getting-started/kafka.md)
- Architecture overview: [`docs/Overview.md`](docs/Overview.md)
- Runtime and transport internals: [`docs/README.md`](docs/README.md)
- API compatibility policy: [`docs/ApiStability.md`](docs/ApiStability.md)

Feature-specific guides:

- lifecycle: [`docs/guides/lifecycle.md`](docs/guides/lifecycle.md)
- error handling: [`docs/guides/error-handling.md`](docs/guides/error-handling.md)
- retry: [`docs/guides/retry.md`](docs/guides/retry.md)
- dead-lettering: [`docs/guides/dead-lettering.md`](docs/guides/dead-lettering.md)
- flow control: [`docs/guides/flow-control.md`](docs/guides/flow-control.md)
- distributed execution: [`docs/guides/distributed-execution.md`](docs/guides/distributed-execution.md)
- performance: [`docs/guides/performance.md`](docs/guides/performance.md)
- operational tooling: [`docs/guides/operational-tooling.md`](docs/guides/operational-tooling.md)

## Project Layout

- [`src/Pipelinez`](src/Pipelinez)
  core runtime
- [`src/Pipelinez.Kafka`](src/Pipelinez.Kafka)
  Kafka transport extension
- [`src/tests/Pipelinez.Tests`](src/tests/Pipelinez.Tests)
  core tests
- [`src/tests/Pipelinez.Kafka.Tests`](src/tests/Pipelinez.Kafka.Tests)
  Kafka integration tests
- [`src/benchmarks/Pipelinez.Benchmarks`](src/benchmarks/Pipelinez.Benchmarks)
  BenchmarkDotNet-based performance benchmarks

## Running Locally

Build the solution:

```bash
dotnet build src/Pipelinez.sln
```

Run the full test suite:

```bash
dotnet test src/Pipelinez.sln
```

Run the benchmark project:

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks
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
- configurable retry, error-handling, and dead-letter flows
- explicit flow control with publish result handling
- distributed runtime mode and worker/partition observability
- partition-aware Kafka scaling
- performance tuning, batching, and runtime performance snapshots
- operational health snapshots, health checks, metrics, and correlation IDs
- Docker-backed Kafka integration coverage
- public API approval tests and repository-level API stability guidance

## API Stability

Pipelinez treats the public API of `Pipelinez` and `Pipelinez.Kafka` as an intentional compatibility contract.

- stable APIs are expected to remain source-compatible within the current major version
- preview APIs should be explicitly marked and documented when introduced
- public API approval tests protect both assemblies from accidental surface changes in normal PR and CI validation

See [`docs/ApiStability.md`](docs/ApiStability.md) for the full policy and maintainer workflow.

## Releases

Pipelinez uses SemVer-style versions and publishes `Pipelinez` and `Pipelinez.Kafka` with aligned package versions.

- stable releases use tags like `v1.2.3`
- preview releases use tags like `v1.3.0-preview.1`
- release notes are tracked in [`CHANGELOG.md`](CHANGELOG.md) and GitHub Releases
- NuGet publishing uses Trusted Publishing rather than a long-lived API key

## Contributing

Contributions are welcome. Start with [`CONTRIBUTING.md`](CONTRIBUTING.md) for local setup, test expectations, and public API guidance.

## License

This repository is licensed under the MIT License. See [`LICENSE`](LICENSE).
