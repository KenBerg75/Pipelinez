# Pipelinez

Typed, observable record-processing pipelines for .NET.

[![NuGet](https://img.shields.io/nuget/v/Pipelinez.svg)](https://www.nuget.org/packages/Pipelinez)
[![NuGet Kafka](https://img.shields.io/nuget/v/Pipelinez.Kafka.svg)](https://www.nuget.org/packages/Pipelinez.Kafka)
[![NuGet Azure Service Bus](https://img.shields.io/nuget/v/Pipelinez.AzureServiceBus.svg)](https://www.nuget.org/packages/Pipelinez.AzureServiceBus)
[![NuGet RabbitMQ](https://img.shields.io/nuget/v/Pipelinez.RabbitMQ.svg)](https://www.nuget.org/packages/Pipelinez.RabbitMQ)
[![NuGet Amazon S3](https://img.shields.io/nuget/v/Pipelinez.AmazonS3.svg)](https://www.nuget.org/packages/Pipelinez.AmazonS3)
[![NuGet PostgreSQL](https://img.shields.io/nuget/v/Pipelinez.PostgreSql.svg)](https://www.nuget.org/packages/Pipelinez.PostgreSql)
[![NuGet SQL Server](https://img.shields.io/nuget/v/Pipelinez.SqlServer.svg)](https://www.nuget.org/packages/Pipelinez.SqlServer)
[![CI](https://github.com/KenBerg75/Pipelinez/actions/workflows/CI.yaml/badge.svg)](https://github.com/KenBerg75/Pipelinez/actions/workflows/CI.yaml)
[![CodeQL](https://github.com/KenBerg75/Pipelinez/actions/workflows/codeql.yaml/badge.svg)](https://github.com/KenBerg75/Pipelinez/actions/workflows/codeql.yaml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/KenBerg75/Pipelinez/badge)](https://scorecard.dev/viewer/?uri=github.com/KenBerg75/Pipelinez)
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
| [`Pipelinez.AzureServiceBus`](https://www.nuget.org/packages/Pipelinez.AzureServiceBus) | Azure Service Bus queue/topic sources, destinations, dead-lettering, and competing-consumer workers | `dotnet add package Pipelinez.AzureServiceBus` |
| [`Pipelinez.RabbitMQ`](https://www.nuget.org/packages/Pipelinez.RabbitMQ) | RabbitMQ queue sources, exchange/queue destinations, dead-lettering, and competing-consumer workers | `dotnet add package Pipelinez.RabbitMQ` |
| [`Pipelinez.AmazonS3`](https://www.nuget.org/packages/Pipelinez.AmazonS3) | Amazon S3 object sources, object destinations, and dead-letter artifact writes | `dotnet add package Pipelinez.AmazonS3` |
| [`Pipelinez.PostgreSql`](https://www.nuget.org/packages/Pipelinez.PostgreSql) | PostgreSQL destination and dead-letter writes with consumer-owned schema mapping | `dotnet add package Pipelinez.PostgreSql` |
| [`Pipelinez.SqlServer`](https://www.nuget.org/packages/Pipelinez.SqlServer) | SQL Server destination and dead-letter writes with consumer-owned schema mapping | `dotnet add package Pipelinez.SqlServer` |

Install the core runtime:

```bash
dotnet add package Pipelinez
```

For Kafka support:

```bash
dotnet add package Pipelinez.Kafka
```

`Pipelinez.Kafka` depends on `Pipelinez`, so Kafka consumers do not need to add both explicitly unless they want to.

For Azure Service Bus support:

```bash
dotnet add package Pipelinez.AzureServiceBus
```

`Pipelinez.AzureServiceBus` depends on `Pipelinez`, so Azure Service Bus consumers do not need to add both explicitly unless they want to.

For RabbitMQ support:

```bash
dotnet add package Pipelinez.RabbitMQ
```

`Pipelinez.RabbitMQ` depends on `Pipelinez`, so RabbitMQ consumers do not need to add both explicitly unless they want to.

For Amazon S3 object source, destination, or dead-letter support:

```bash
dotnet add package Pipelinez.AmazonS3
```

`Pipelinez.AmazonS3` depends on `Pipelinez`, so Amazon S3 consumers do not need to add both explicitly unless they want to.

For PostgreSQL destination or dead-letter support:

```bash
dotnet add package Pipelinez.PostgreSql
```

`Pipelinez.PostgreSql` also depends on `Pipelinez`, so PostgreSQL consumers do not need to add both explicitly unless they want to.

For SQL Server destination or dead-letter support:

```bash
dotnet add package Pipelinez.SqlServer
```

`Pipelinez.SqlServer` also depends on `Pipelinez`, so SQL Server consumers do not need to add both explicitly unless they want to.

Public package publishing is configured through GitHub tag releases and NuGet Trusted Publishing.

The published packages include XML IntelliSense documentation, so the public API descriptions are available directly in editors like Visual Studio and Rider. Browsable generated API documentation is published at [`kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html`](https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html).

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
- Azure Service Bus queue/topic source, destination, and dead-letter transport support
- RabbitMQ queue source, exchange/queue destination, and dead-letter transport support
- Amazon S3 object source, object destination, and dead-letter artifact support
- PostgreSQL destination and dead-letter transport support
- SQL Server destination and dead-letter transport support
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

- [`documentation/getting-started/kafka.md`](documentation/getting-started/kafka.md)
- [`src/examples/Example.Kafka`](src/examples/Example.Kafka)
- [`src/examples/Example.Kafka.DataGen`](src/examples/Example.Kafka.DataGen)
- [`src/tests/Pipelinez.Kafka.Tests`](src/tests/Pipelinez.Kafka.Tests)

## Azure Service Bus Support

Azure Service Bus support lives in the separate `Pipelinez.AzureServiceBus` package and adds:

- queue source support
- topic subscription source support
- queue and topic destination support
- Pipelinez dead-letter destinations
- competing-consumer distributed worker support

Example shape:

```csharp
using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.Core;

var connection = new AzureServiceBusConnectionOptions
{
    ConnectionString = "Endpoint=sb://..."
};

var pipeline = Pipeline<MyRecord>.New("asb-pipeline")
    .WithAzureServiceBusSource(
        new AzureServiceBusSourceOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-in")
        },
        message => new MyRecord
        {
            Id = message.MessageId,
            Value = message.Body.ToString()
        })
    .WithAzureServiceBusDestination(
        new AzureServiceBusDestinationOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-out")
        },
        record => new ServiceBusMessage(BinaryData.FromString(record.Value))
        {
            MessageId = record.Id
        })
    .Build();
```

For a full walkthrough, see:

- [`documentation/getting-started/azure-service-bus.md`](documentation/getting-started/azure-service-bus.md)
- [`documentation/transports/azure-service-bus.md`](documentation/transports/azure-service-bus.md)
- [`src/examples/Example.AzureServiceBus`](src/examples/Example.AzureServiceBus)
- [`src/tests/Pipelinez.AzureServiceBus.Tests`](src/tests/Pipelinez.AzureServiceBus.Tests)

## RabbitMQ Support

RabbitMQ support lives in the separate `Pipelinez.RabbitMQ` package and adds:

- queue source support
- exchange and routing-key destination support
- default-exchange queue publishing
- Pipelinez dead-letter destinations
- competing-consumer distributed worker support
- manual ack/nack source settlement after terminal Pipelinez handling

Example shape:

```csharp
using System.Text;
using Pipelinez.Core;
using Pipelinez.RabbitMQ;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Destination;

var connection = new RabbitMqConnectionOptions
{
    Uri = new Uri("amqp://guest:guest@localhost:5672/")
};

var pipeline = Pipeline<MyRecord>.New("rabbitmq-pipeline")
    .WithRabbitMqSource(
        new RabbitMqSourceOptions
        {
            Connection = connection,
            Queue = RabbitMqQueueOptions.Named("orders-in")
        },
        delivery => new MyRecord
        {
            Id = delivery.Properties?.MessageId ?? delivery.DeliveryTag.ToString(),
            Value = Encoding.UTF8.GetString(delivery.Body.Span)
        })
    .WithRabbitMqDestination(
        new RabbitMqDestinationOptions
        {
            Connection = connection,
            RoutingKey = "orders-out"
        },
        record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(record.Value)))
    .Build();
```

For a full walkthrough, see:

- [`documentation/getting-started/rabbitmq.md`](documentation/getting-started/rabbitmq.md)
- [`documentation/transports/rabbitmq.md`](documentation/transports/rabbitmq.md)
- [`src/examples/Example.RabbitMQ`](src/examples/Example.RabbitMQ)
- [`src/tests/Pipelinez.RabbitMQ.Tests`](src/tests/Pipelinez.RabbitMQ.Tests)

## Amazon S3 Support

Amazon S3 support lives in the separate `Pipelinez.AmazonS3` package in this repository and currently focuses on:

- S3 object enumeration source support
- S3 object destination writes
- S3 dead-letter artifact writes
- source object settlement through leave, delete, tag, copy, or move actions
- S3-compatible endpoint configuration for LocalStack-style development

Example shape:

```csharp
using System.Text.Json;
using Pipelinez.AmazonS3;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Destination;
using Pipelinez.Core;

var pipeline = Pipeline<MyRecord>.New("s3-pipeline")
    .WithInMemorySource(new object())
    .WithAmazonS3Destination(
        new AmazonS3DestinationOptions
        {
            Connection = new AmazonS3ConnectionOptions { Region = "us-east-1" },
            BucketName = "processed-orders",
            Write = new AmazonS3ObjectWriteOptions { KeyPrefix = "orders/" }
        },
        record => AmazonS3PutObject.FromText(
            $"{record.Id}.json",
            JsonSerializer.Serialize(record),
            "application/json"))
    .Build();
```

For more detail, see:

- [`documentation/transports/amazon-s3.md`](documentation/transports/amazon-s3.md)
- [`src/examples/Example.AmazonS3`](src/examples/Example.AmazonS3)
- [`src/tests/Pipelinez.AmazonS3.Tests`](src/tests/Pipelinez.AmazonS3.Tests)

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

## SQL Server Support

SQL Server support lives in the separate `Pipelinez.SqlServer` package in this repository and currently focuses on:

- SQL Server destination writes
- SQL Server dead-letter writes
- consumer-owned schema and table mapping
- custom parameterized SQL through Dapper-backed execution

Example shape:

```csharp
using Pipelinez.Core;
using Pipelinez.SqlServer;
using Pipelinez.SqlServer.Configuration;
using Pipelinez.SqlServer.Mapping;

var pipeline = Pipeline<MyRecord>.New("sql-server-pipeline")
    .WithInMemorySource(new object())
    .WithSqlServerDestination(
        new SqlServerDestinationOptions
        {
            ConnectionString = "Server=localhost;Database=pipelinez;User Id=sa;Password=P@ssw0rd!2026;TrustServerCertificate=True"
        },
        SqlServerTableMap<MyRecord>.ForTable("app", "processed_records")
            .Map("record_id", record => record.Id)
            .MapJson("payload", record => record))
    .Build();
```

## Learn More

- New to Pipelinez: [`documentation/getting-started/in-memory.md`](documentation/getting-started/in-memory.md)
- Using Kafka: [`documentation/getting-started/kafka.md`](documentation/getting-started/kafka.md)
- Using Azure Service Bus: [`documentation/getting-started/azure-service-bus.md`](documentation/getting-started/azure-service-bus.md)
- Using RabbitMQ: [`documentation/getting-started/rabbitmq.md`](documentation/getting-started/rabbitmq.md)
- Using Amazon S3: [`documentation/transports/amazon-s3.md`](documentation/transports/amazon-s3.md)
- Using PostgreSQL destinations: [`documentation/getting-started/postgresql-destination.md`](documentation/getting-started/postgresql-destination.md)
- Using SQL Server destinations: [`documentation/getting-started/sql-server-destination.md`](documentation/getting-started/sql-server-destination.md)
- Architecture overview: [`documentation/Overview.md`](documentation/Overview.md)
- Runtime and transport internals: [`documentation/README.md`](documentation/README.md)
- API reference: [`kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html`](https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html)
- API compatibility policy: [`documentation/ApiStability.md`](documentation/ApiStability.md)

Feature-specific guides:

- lifecycle: [`documentation/guides/lifecycle.md`](documentation/guides/lifecycle.md)
- error handling: [`documentation/guides/error-handling.md`](documentation/guides/error-handling.md)
- retry: [`documentation/guides/retry.md`](documentation/guides/retry.md)
- dead-lettering: [`documentation/guides/dead-lettering.md`](documentation/guides/dead-lettering.md)
- flow control: [`documentation/guides/flow-control.md`](documentation/guides/flow-control.md)
- distributed execution: [`documentation/guides/distributed-execution.md`](documentation/guides/distributed-execution.md)
- performance: [`documentation/guides/performance.md`](documentation/guides/performance.md)
- operational tooling: [`documentation/guides/operational-tooling.md`](documentation/guides/operational-tooling.md)

## Project Layout

- [`src/Pipelinez`](src/Pipelinez)
  core runtime
- [`src/Pipelinez.Kafka`](src/Pipelinez.Kafka)
  Kafka transport extension
- [`src/Pipelinez.AzureServiceBus`](src/Pipelinez.AzureServiceBus)
  Azure Service Bus transport extension
- [`src/Pipelinez.RabbitMQ`](src/Pipelinez.RabbitMQ)
  RabbitMQ transport extension
- [`src/Pipelinez.AmazonS3`](src/Pipelinez.AmazonS3)
  Amazon S3 object transport extension
- [`src/Pipelinez.PostgreSql`](src/Pipelinez.PostgreSql)
  PostgreSQL destination and dead-letter transport extension
- [`src/Pipelinez.SqlServer`](src/Pipelinez.SqlServer)
  SQL Server destination and dead-letter transport extension
- [`src/tests/Pipelinez.Tests`](src/tests/Pipelinez.Tests)
  core tests
- [`src/tests/Pipelinez.Kafka.Tests`](src/tests/Pipelinez.Kafka.Tests)
  Kafka integration tests
- [`src/tests/Pipelinez.AzureServiceBus.Tests`](src/tests/Pipelinez.AzureServiceBus.Tests)
  Azure Service Bus transport and approval tests
- [`src/tests/Pipelinez.RabbitMQ.Tests`](src/tests/Pipelinez.RabbitMQ.Tests)
  RabbitMQ transport, integration, and approval tests
- [`src/tests/Pipelinez.AmazonS3.Tests`](src/tests/Pipelinez.AmazonS3.Tests)
  Amazon S3 transport, integration, and approval tests
- [`src/tests/Pipelinez.PostgreSql.Tests`](src/tests/Pipelinez.PostgreSql.Tests)
  PostgreSQL integration and approval tests
- [`src/tests/Pipelinez.SqlServer.Tests`](src/tests/Pipelinez.SqlServer.Tests)
  SQL Server integration and approval tests
- [`src/benchmarks/Pipelinez.Benchmarks`](src/benchmarks/Pipelinez.Benchmarks)
  BenchmarkDotNet-based performance benchmarks
- [`docs`](docs)
  DocFX configuration for generated API documentation and the GitHub Pages site
- [`documentation`](documentation)
  conceptual documentation source used by the generated site

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

Run Docker-backed transport benchmarks:

```bash
PIPELINEZ_BENCH_ENABLE_DOCKER=true dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*Kafka*"
```

Run Azure Service Bus live benchmarks:

```bash
PIPELINEZ_ASB_CONNECTION_STRING="<connection string>" dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*AzureServiceBus*"
```

Run the Kafka example:

```bash
dotnet run --project src/examples/Example.Kafka
```

Run the Kafka data generator:

```bash
dotnet run --project src/examples/Example.Kafka.DataGen
```

Run the Azure Service Bus example:

```bash
dotnet run --project src/examples/Example.AzureServiceBus
```

Run the RabbitMQ example:

```bash
dotnet run --project src/examples/Example.RabbitMQ
```

Run the Amazon S3 example:

```bash
dotnet run --project src/examples/Example.AmazonS3
```

Run the SQL Server example:

```bash
dotnet run --project src/examples/Example.SqlServer
```

The Kafka, RabbitMQ, Amazon S3, SQL Server examples and Docker-backed integration tests use Docker/Testcontainers for local infrastructure unless you provide existing services through environment variables.

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
- Azure Service Bus transport unit and API approval coverage
- RabbitMQ transport unit, API approval, and Docker-backed integration coverage when Docker is available
- Amazon S3 transport unit, API approval, and Docker-backed LocalStack integration coverage when Docker is available
- Docker-backed PostgreSQL destination and dead-letter integration coverage
- Docker-backed SQL Server destination and dead-letter integration coverage
- public API approval tests and repository-level API stability guidance
- Dependabot, Dependency Review, CodeQL, OpenSSF Scorecard, and release SBOM automation

## API Stability

Pipelinez treats the public API of `Pipelinez`, `Pipelinez.Kafka`, `Pipelinez.AzureServiceBus`, `Pipelinez.RabbitMQ`, `Pipelinez.AmazonS3`, `Pipelinez.PostgreSql`, and `Pipelinez.SqlServer` as an intentional compatibility contract.

- stable APIs are expected to remain source-compatible within the current major version
- preview APIs should be explicitly marked and documented when introduced
- public API approval tests protect package assemblies from accidental surface changes in normal PR and CI validation

See [`documentation/ApiStability.md`](documentation/ApiStability.md) for the full policy and maintainer workflow.

## Releases

Pipelinez uses SemVer-style versions and publishes `Pipelinez`, `Pipelinez.Kafka`, `Pipelinez.AzureServiceBus`, `Pipelinez.RabbitMQ`, `Pipelinez.AmazonS3`, `Pipelinez.PostgreSql`, and `Pipelinez.SqlServer` with aligned package versions.

- stable releases use tags like `v1.2.3`
- preview releases use tags like `v1.3.0-preview.1`
- release notes are tracked in [`CHANGELOG.md`](CHANGELOG.md) and GitHub Releases
- NuGet publishing uses Trusted Publishing rather than a long-lived API key

## Contributing

Contributions are welcome. Start with [`CONTRIBUTING.md`](CONTRIBUTING.md) for local setup, test expectations, and public API guidance.

## License

This repository is licensed under the MIT License. See [`LICENSE`](LICENSE).
