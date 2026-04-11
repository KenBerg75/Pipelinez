# Pipelinez

Typed, observable record-processing pipelines for .NET.

[![NuGet](https://img.shields.io/nuget/v/Pipelinez.svg)](https://www.nuget.org/packages/Pipelinez)
[![NuGet Kafka](https://img.shields.io/nuget/v/Pipelinez.Kafka.svg)](https://www.nuget.org/packages/Pipelinez.Kafka)
[![NuGet PostgreSQL](https://img.shields.io/nuget/v/Pipelinez.PostgreSql.svg)](https://www.nuget.org/packages/Pipelinez.PostgreSql)
[![CI](https://github.com/KenBerg75/Pipelinez/actions/workflows/CI.yaml/badge.svg)](https://github.com/KenBerg75/Pipelinez/actions/workflows/CI.yaml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

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

The public packages are available on NuGet.org:

| Package | Purpose | Install |
| --- | --- | --- |
| [`Pipelinez`](https://www.nuget.org/packages/Pipelinez) | Core typed pipeline runtime | `dotnet add package Pipelinez` |
| [`Pipelinez.Kafka`](https://www.nuget.org/packages/Pipelinez.Kafka) | Kafka source, destination, dead-lettering, distributed execution, and partition-aware scaling | `dotnet add package Pipelinez.Kafka` |
| [`Pipelinez.PostgreSql`](https://www.nuget.org/packages/Pipelinez.PostgreSql) | PostgreSQL destination and dead-letter writes with consumer-owned schema mapping | `dotnet add package Pipelinez.PostgreSql` |

Install the core runtime:

```bash
dotnet add package Pipelinez
```

For Kafka support:

```bash
dotnet add package Pipelinez.Kafka
```

`Pipelinez.Kafka` depends on `Pipelinez`, so Kafka consumers do not need to add both explicitly unless they want to.

For PostgreSQL destination or dead-letter support:

```bash
dotnet add package Pipelinez.PostgreSql
```

`Pipelinez.PostgreSql` also depends on `Pipelinez`, so PostgreSQL consumers do not need to add both explicitly unless they want to.

Public package publishing is configured through GitHub tag releases and NuGet Trusted Publishing.

The published packages include XML IntelliSense documentation, so the public API descriptions are available directly in editors like Visual Studio and Rider. Browsable generated API documentation is published at [`kenberg75.github.io/Pipelinez/api`](https://kenberg75.github.io/Pipelinez/api/).

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
- PostgreSQL destination and dead-letter transport support
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

## PostgreSQL Support

PostgreSQL support lives in the separate `Pipelinez.PostgreSql` package in this repository and currently focuses on:

- PostgreSQL destination writes
- PostgreSQL dead-letter writes
- consumer-owned schema and table mapping
- custom parameterized SQL through Dapper-backed execution

Example shape:

```csharp
using Pipelinez.Core;
using Pipelinez.PostgreSql;
using Pipelinez.PostgreSql.Configuration;
using Pipelinez.PostgreSql.Mapping;

var pipeline = Pipeline<MyRecord>.New("postgres-pipeline")
    .WithInMemorySource(new object())
    .WithPostgreSqlDestination(
        new PostgreSqlDestinationOptions
        {
            ConnectionString = "Host=localhost;Database=pipelinez;Username=postgres;Password=postgres"
        },
        PostgreSqlTableMap<MyRecord>.ForTable("app", "processed_records")
            .Map("record_id", record => record.Id)
            .MapJson("payload", record => record))
    .Build();
```

## Learn More

- New to Pipelinez: [`docs/getting-started/in-memory.md`](docs/getting-started/in-memory.md)
- Using Kafka: [`docs/getting-started/kafka.md`](docs/getting-started/kafka.md)
- Using PostgreSQL destinations: [`docs/getting-started/postgresql-destination.md`](docs/getting-started/postgresql-destination.md)
- Architecture overview: [`docs/Overview.md`](docs/Overview.md)
- Runtime and transport internals: [`docs/README.md`](docs/README.md)
- API reference: [`kenberg75.github.io/Pipelinez/api`](https://kenberg75.github.io/Pipelinez/api/)
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
- [`src/Pipelinez.PostgreSql`](src/Pipelinez.PostgreSql)
  PostgreSQL destination and dead-letter transport extension
- [`src/tests/Pipelinez.Tests`](src/tests/Pipelinez.Tests)
  core tests
- [`src/tests/Pipelinez.Kafka.Tests`](src/tests/Pipelinez.Kafka.Tests)
  Kafka integration tests
- [`src/tests/Pipelinez.PostgreSql.Tests`](src/tests/Pipelinez.PostgreSql.Tests)
  PostgreSQL integration and approval tests
- [`src/benchmarks/Pipelinez.Benchmarks`](src/benchmarks/Pipelinez.Benchmarks)
  BenchmarkDotNet-based performance benchmarks
- [`docs-site`](docs-site)
  DocFX configuration for generated API documentation and the GitHub Pages site

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
- Docker-backed PostgreSQL destination and dead-letter integration coverage
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
