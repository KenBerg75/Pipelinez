# Pipelinez

Typed data pipelines for .NET.

Pipelinez is a small .NET 8 framework for building record-processing pipelines with a consistent runtime model:

- strongly typed records
- pluggable sources, segments, and destinations
- async startup and completion
- fault tracking and configurable error policies
- configurable retry policies for segments and destinations
- dead-letter destinations for preserving failed records
- explicit flow control and saturation observability
- optional distributed execution for transport-backed sources
- explicit performance tuning and runtime performance snapshots
- health snapshots, health checks, and meter-based runtime metrics
- correlation-aware diagnostics for records and faults
- transport extensions such as Kafka

## Installation

Package distribution is now configured for:

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

Public publishing and release/version automation are tracked separately from the packaging work itself.

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
- retry history

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
- `WithKafkaDeadLetterDestination(...)`

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

Kafka source configuration now also supports explicit partition-aware scaling through `KafkaPartitionScalingOptions`, including:

- preserving order within a partition
- parallelizing across owned partitions
- opt-in relaxed ordering within a partition
- partition drain visibility during rebalance

## Distributed Execution

Pipelinez can run in `SingleProcess` mode or in explicit `Distributed` mode for distributed-capable sources such as Kafka.

In distributed mode, the runtime surfaces:

- worker identity through `PipelineHostOptions`
- current owned partitions through `GetRuntimeContext()`
- current partition execution state through `GetRuntimeContext()` and `GetStatus()`
- worker lifecycle and rebalance events
- partition drain and partition execution-state events
- record-level distribution context on completion and fault events

Example shape:

```csharp
using Pipelinez.Core.Distributed;

var pipeline = Pipeline<MyRecord>.New("orders")
    .UseHostOptions(new PipelineHostOptions
    {
        ExecutionMode = PipelineExecutionMode.Distributed,
        InstanceId = Environment.MachineName,
        WorkerId = $"orders-{Guid.NewGuid():N}"
    })
    .WithKafkaSource(...)
    .WithKafkaDestination(...)
    .Build();

pipeline.OnPartitionsAssigned += (_, args) =>
{
    Console.WriteLine(
        $"Worker {args.RuntimeContext.WorkerId} now owns {args.RuntimeContext.OwnedPartitions.Count} partitions.");
};
```

Kafka-backed distributed execution is validated by multi-worker integration tests that scale workers in and out against a real Docker-hosted broker.

For Kafka specifically, distributed execution now also exposes partition-aware scaling controls through `KafkaPartitionScalingOptions` so callers can choose whether to preserve partition order strictly or relax within-partition ordering deliberately.

## Performance Tuning

Pipelinez now exposes explicit throughput and execution controls through `UsePerformanceOptions(...)`.

Available tuning areas include:

- source, segment, and destination bounded capacity
- segment degree of parallelism
- ordered versus unordered segment execution
- optional destination batching for batched destinations
- runtime performance snapshots through `GetPerformanceSnapshot()`

Example shape:

```csharp
using Pipelinez.Core.Performance;

var pipeline = Pipeline<MyRecord>.New("high-throughput")
    .UsePerformanceOptions(new PipelinePerformanceOptions
    {
        DefaultSegmentExecution = new PipelineExecutionOptions
        {
            BoundedCapacity = 10_000,
            DegreeOfParallelism = Environment.ProcessorCount,
            EnsureOrdered = false
        }
    })
    .WithInMemorySource(new object())
    .AddSegment(new MySegment(), new object())
    .WithInMemoryDestination("config")
    .Build();

var snapshot = pipeline.GetPerformanceSnapshot();
Console.WriteLine(snapshot.RecordsPerSecond);
```

Increasing parallelism or disabling ordering can improve throughput, but it can also change observable processing order. Those settings should be treated as explicit tradeoffs rather than passive defaults.

## Flow Control

Pipelinez now exposes explicit publish-time flow-control behavior in addition to component `BoundedCapacity` tuning.

Flow control can be configured through:

- `UseFlowControlOptions(...)`
- `PublishAsync(record, PipelinePublishOptions)`

Supported overflow behaviors include:

- `PipelineOverflowPolicy.Wait`
- `PipelineOverflowPolicy.Reject`
- `PipelineOverflowPolicy.Cancel`

Example shape:

```csharp
using Pipelinez.Core.FlowControl;

var pipeline = Pipeline<MyRecord>.New("orders")
    .UsePerformanceOptions(new PipelinePerformanceOptions
    {
        SourceExecution = new PipelineExecutionOptions { BoundedCapacity = 100 },
        DestinationExecution = new PipelineExecutionOptions { BoundedCapacity = 100 }
    })
    .UseFlowControlOptions(new PipelineFlowControlOptions
    {
        OverflowPolicy = PipelineOverflowPolicy.Wait,
        PublishTimeout = TimeSpan.FromSeconds(5),
        SaturationWarningThreshold = 0.8
    })
    .WithInMemorySource(new object())
    .WithInMemoryDestination("config")
    .Build();

var publishResult = await pipeline.PublishAsync(
    new MyRecord(),
    new PipelinePublishOptions
    {
        OverflowPolicyOverride = PipelineOverflowPolicy.Reject
    });

if (!publishResult.Accepted)
{
    Console.WriteLine($"Publish was not accepted: {publishResult.Reason}");
}
```

The runtime now also surfaces flow pressure through `GetStatus().FlowControlStatus`, `OnSaturationChanged`, `OnPublishRejected`, and publish wait/rejection counters in `GetPerformanceSnapshot()`.

## Retry Policies

Pipelinez supports explicit retry policies for transient failures in segments and destinations.

Retry configuration can be applied:

- pipeline-wide through `UseRetryOptions(...)`
- per segment through `AddSegment(..., retryPolicy)`
- per destination through `WithDestination(..., retryPolicy)`

Available policy styles include:

- `PipelineRetryPolicy<T>.None()`
- `PipelineRetryPolicy<T>.FixedDelay(...)`
- `PipelineRetryPolicy<T>.ExponentialBackoff(...)`

Example shape:

```csharp
using Pipelinez.Core.Retry;

var pipeline = Pipeline<MyRecord>.New("orders")
    .UseRetryOptions(new PipelineRetryOptions<MyRecord>
    {
        DefaultSegmentPolicy = PipelineRetryPolicy<MyRecord>
            .ExponentialBackoff(
                maxAttempts: 5,
                initialDelay: TimeSpan.FromMilliseconds(100),
                maxDelay: TimeSpan.FromSeconds(3),
                useJitter: true)
            .Handle<TimeoutException>(),
        DestinationPolicy = PipelineRetryPolicy<MyRecord>.FixedDelay(
            maxAttempts: 3,
            delay: TimeSpan.FromSeconds(1))
    })
    .WithInMemorySource(new object())
    .AddSegment(new MySegment(), new object())
    .WithInMemoryDestination("config")
    .Build();

pipeline.OnPipelineRecordRetrying += (_, args) =>
{
    Console.WriteLine(
        $"{args.ComponentName} retry {args.AttemptNumber}/{args.MaxAttempts} for {args.Record.Id}");
};
```

Retry exhaustion flows into the existing `WithErrorHandler(...)` path, so consumers can still decide whether to skip, stop, or rethrow once the configured retry policy has been exhausted.

## Dead-Lettering

Pipelinez supports first-class dead-letter handling for faulted records.

Dead-letter configuration can be applied through:

- `WithDeadLetterDestination(...)`
- `UseDeadLetterOptions(...)`
- `WithKafkaDeadLetterDestination(...)`

When an error handler returns `PipelineErrorAction.DeadLetter`, the runtime:

- preserves the failed record in a `PipelineDeadLetterRecord<T>`
- includes fault state, metadata, segment history, retry history, and distribution context
- writes that envelope to the configured dead-letter destination
- continues processing later records if the dead-letter write succeeds

Example shape:

```csharp
using Pipelinez.Core.DeadLettering;

var deadLetters = new InMemoryDeadLetterDestination<MyRecord>();

var pipeline = Pipeline<MyRecord>.New("orders")
    .WithInMemorySource(new object())
    .AddSegment(new MySegment(), new object())
    .WithInMemoryDestination("config")
    .WithDeadLetterDestination(deadLetters)
    .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
    .Build();

pipeline.OnPipelineRecordDeadLettered += (_, args) =>
{
    Console.WriteLine(
        $"Dead-lettered {args.Record.Id} from {args.DeadLetterRecord.Fault.ComponentName}");
};
```

Kafka-backed pipelines can route failures to a dead-letter topic through `WithKafkaDeadLetterDestination(...)`.

## Operational Tooling

Pipelinez now includes a first-class operational surface for hosts and operators.

Available capabilities include:

- `GetHealthStatus()`
- `GetOperationalSnapshot()`
- `PipelineHealthCheck<T>` for `Microsoft.Extensions.Diagnostics.HealthChecks`
- meter-based runtime metrics under `Pipelinez.Runtime`
- correlation IDs stamped into pipeline metadata and surfaced through event diagnostics

Example shape:

```csharp
using Pipelinez.Core.Operational;

var pipeline = Pipeline<MyRecord>.New("orders")
    .UseOperationalOptions(new PipelineOperationalOptions
    {
        EnableHealthChecks = true,
        EnableMetrics = true,
        EnableCorrelationIds = true
    })
    .WithInMemorySource(new object())
    .WithInMemoryDestination("config")
    .Build();

var health = pipeline.GetHealthStatus();
var snapshot = pipeline.GetOperationalSnapshot();

Console.WriteLine(health.State);
Console.WriteLine(snapshot.Performance.RecordsPerSecond);
```

For OpenTelemetry-style metrics registration, add the `Pipelinez.Runtime` meter to your metrics pipeline.

## Error Handling

Pipelinez supports explicit fault handling through `WithErrorHandler(...)`.

Available actions:

- `SkipRecord`
  continue processing later records
- `DeadLetter`
  preserve the failed record through the configured dead-letter destination and continue when that write succeeds
- `StopPipeline`
  fault and stop the pipeline
- `Rethrow`
  fault the pipeline and surface the original exception

Public events include:

- `OnPipelineRecordRetrying`
- `OnSaturationChanged`
- `OnPublishRejected`
- `OnPipelineRecordCompleted`
- `OnPipelineRecordFaulted`
- `OnPipelineRecordDeadLettered`
- `OnPipelineDeadLetterWriteFailed`
- `OnPipelineFaulted`
- `OnWorkerStarted`
- `OnPartitionsAssigned`
- `OnPartitionsRevoked`
- `OnWorkerStopping`

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
- [`docs/README.md`](docs/README.md)
  documentation index and guide map
- [`docs/Overview.md`](docs/Overview.md)
  deeper architectural overview
- [`docs/ApiStability.md`](docs/ApiStability.md)
  compatibility, stability, and public API change guidance

## Documentation

For task-oriented docs, start with:

- [`docs/README.md`](docs/README.md)
- [`docs/getting-started/in-memory.md`](docs/getting-started/in-memory.md)
- [`docs/getting-started/kafka.md`](docs/getting-started/kafka.md)

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

## Package Validation

Generate local packages:

```bash
dotnet pack src/Pipelinez/Pipelinez.csproj -c Release -o artifacts/packages -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
dotnet pack src/Pipelinez.Kafka/Pipelinez.Kafka.csproj -c Release -o artifacts/packages -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
```

Run the local package smoke test:

```powershell
./scripts/Validate-Packages.ps1 -PackageDirectory artifacts/packages
```

## Status

Current implemented capabilities include:

- async pipeline lifecycle
- fault-aware record containers
- segment execution history
- configurable error policies
- configurable retry policies with retry events and retry history
- dead-letter destinations, dead-letter events, and dead-letter performance counters
- configurable flow-control policies with saturation status and publish result handling
- async destination execution
- distributed runtime mode and worker/partition observability
- partition-aware Kafka scaling with partition execution state and drain events
- performance tuning options, batching support, and runtime performance snapshots
- operational health snapshots, health-check integration, runtime meter metrics, and correlation IDs
- Kafka source and destination support
- Docker-backed Kafka integration coverage, including multi-worker distributed tests
- public API approval tests and repository-level API stability guidance

## API Stability

Pipelinez now treats the public API of `Pipelinez` and `Pipelinez.Kafka` as an intentional compatibility contract.

- stable APIs are expected to remain source-compatible within the current major version
- preview APIs should be explicitly marked and documented when introduced
- public API approval tests protect both assemblies from accidental surface changes in normal PR and CI validation

See [`docs/ApiStability.md`](docs/ApiStability.md) for the full policy and maintainer workflow.

## License

This repository is licensed under the MIT License. See [`LICENSE`](LICENSE).
