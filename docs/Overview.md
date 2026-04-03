# Overview

## What Pipelinez Is

Pipelinez is a .NET 8 pipeline framework for moving typed records through a consistent runtime model:

- `PipelineRecord`
  the user-defined payload base type
- `IPipelineSource<T>`
  introduces records into the pipeline
- `IPipelineSegment<T>`
  transforms records in the middle of the pipeline
- `IPipelineDestination<T>`
  consumes records at the end of the pipeline

The runtime is built on `System.Threading.Tasks.Dataflow`. A pipeline is linked as:

`source -> segment 1 -> segment 2 -> ... -> destination`

Each record flows through the runtime inside a `PipelineContainer<T>`, which lets the framework carry payload, metadata, fault state, and execution history together.

## Solution Layout

- `src/Pipelinez.sln`
  the main solution containing the core library, Kafka extension library, tests, and examples
- `src/Pipelinez`
  the transport-agnostic pipeline runtime
- `src/Pipelinez.Kafka`
  the Kafka transport extension assembly
- `src/tests/Pipelinez.Tests`
  unit and runtime tests for core pipeline behavior
- `src/tests/Pipelinez.Kafka.Tests`
  Docker-backed Kafka integration tests using Testcontainers
- `src/benchmarks/Pipelinez.Benchmarks`
  BenchmarkDotNet project for repeatable in-memory performance measurements
- `src/examples/Example.Kafka`
  sample application that builds a Kafka-backed pipeline
- `src/examples/Example.Kafka.DataGen`
  simple Kafka publisher used to generate example traffic

## Core Runtime Design

### Pipeline Construction

The entry point is `Pipeline<T>.New("name")`, which returns `PipelineBuilder<T>`.

The core builder currently supports:

- `WithSource(...)`
- `WithInMemorySource(...)`
- `AddSegment(...)`
- `WithDestination(...)`
- `WithInMemoryDestination(...)`
- `UseHostOptions(...)`
- `UsePerformanceOptions(...)`
- `UseLogger(...)`
- `WithErrorHandler(...)`

Kafka integrates through extension methods in `Pipelinez.Kafka`, not through partial builder types. The Kafka assembly adds:

- `WithKafkaSource(...)`
- `WithKafkaDestination(...)`

`Build()` validates that a source and destination exist, creates a `Pipeline<T>`, links all blocks, and initializes the source and destination.
If distributed execution is requested, `Build()` also validates that the configured source implements the distributed source contract.
Performance options are resolved at build time and applied to sources, segments, and destinations before the dataflow blocks are linked.

### Record Model

`PipelineRecord` is the base class for all pipeline payloads. It only carries `Headers`, leaving payload shape entirely to the caller.

`PipelineContainer<T>` wraps each record and carries:

- `Record`
  the current typed payload
- `Metadata`
  integration metadata such as Kafka topic, partition, and offset
- `Fault`
  a `PipelineFaultState` when execution has faulted
- `HasFault`
  convenience flag for fault checks
- `SegmentHistory`
  ordered `PipelineSegmentExecution` entries that capture segment execution results

This container is the runtime boundary object shared by sources, segments, destinations, error handling, and transport adapters.

### Source Behavior

Sources derive from `PipelineSourceBase<T>`, which owns a `BufferBlock<PipelineContainer<T>>`.

Responsibilities:

- publish records manually through `PublishAsync`
- optionally produce records from an external system inside `MainLoop(...)`
- link to the next pipeline component
- observe completed containers through `OnPipelineContainerComplete(...)`
- accept configurable bounded-capacity execution options
- record source-side publish metrics for performance snapshots

`InMemoryPipelineSource<T>` is effectively a passive source. Kafka-backed sources actively consume from Kafka and publish into the pipeline.

### Segment Behavior

Segments derive from `PipelineSegment<T>`, which wraps a `TransformBlock<PipelineContainer<T>, PipelineContainer<T>>`.

For each container:

1. the segment checks whether the container already has a fault
2. the segment executes `ExecuteAsync(T arg)`
3. the returned record replaces `PipelineContainer<T>.Record`
4. the segment appends a `PipelineSegmentExecution` entry to `SegmentHistory`
5. if an exception occurs, the segment marks the container faulted and allows downstream error handling to decide what to do

This keeps the segment authoring model simple while still preserving runtime-level observability.
Segments now also support configurable `DegreeOfParallelism`, `BoundedCapacity`, and `EnsureOrdered` behavior through `PipelineExecutionOptions`.

### Destination Behavior

Destinations derive from `PipelineDestination<T>`, which owns a `BufferBlock<PipelineContainer<T>>`.

The destination loop:

1. receives completed containers from upstream
2. checks for pre-faulted containers
3. delegates fault-policy decisions back to the pipeline when needed
4. executes `ExecuteAsync(T record, CancellationToken cancellationToken)` for successful containers
5. raises container-completed and record-completed events only after successful destination execution

Destination execution is fully async, and destination `Completion` now represents the full destination work lifecycle rather than only message-buffer completion.
Destinations also support optional batched execution when they implement `IBatchedPipelineDestination<T>` and batching is enabled through `PipelinePerformanceOptions`.

## Execution Lifecycle

The runtime lifecycle is:

1. build the pipeline
2. call `StartPipelineAsync(CancellationToken)`
3. publish records or let the source run
4. call `CompleteAsync()` when no more records should enter the pipeline
5. optionally await `pipeline.Completion`

Important current semantics:

- `StartPipelineAsync(...)` returns `Task`
- starting twice throws
- publishing before start throws
- completing before start throws
- `Completion` represents the pipeline run, not just one internal block
- `CompleteAsync()` waits for downstream destination work before final completion

`Pipeline<T>` also tracks runtime state explicitly:

- `NotStarted`
- `Starting`
- `Running`
- `Completing`
- `Completed`
- `Faulted`

## Performance Model

Pipelinez now has a first-class runtime performance model.

### Performance Options

`UsePerformanceOptions(...)` configures:

- source execution options
- default segment execution options
- destination execution options
- destination batching
- runtime metrics collection behavior

The core execution type is `PipelineExecutionOptions`, which controls:

- `BoundedCapacity`
- `DegreeOfParallelism`
- `EnsureOrdered`

Segment defaults remain conservative, but callers can now opt into higher-throughput settings explicitly.

### Performance Snapshot

`Pipeline<T>.GetPerformanceSnapshot()` returns a `PipelinePerformanceSnapshot` containing:

- elapsed runtime
- total published, completed, and faulted record counts
- calculated records-per-second
- average end-to-end latency
- per-component performance snapshots

Per-component snapshots expose:

- component name
- processed count
- faulted count
- records-per-second
- average execution latency

This gives consumers a lightweight built-in way to inspect pipeline behavior during testing, demos, or hosted execution.

### Destination Batching

Batch-capable destinations can implement `IBatchedPipelineDestination<T>`.

When batching is enabled:

- records are accumulated up to the configured batch size
- the batch is flushed early when the max batch delay is reached
- remaining records are flushed on normal completion
- completion events are only raised after the batch succeeds
- batch failures are converted into per-record fault handling so the existing error-policy model remains in control

This is intended for throughput-oriented destinations and should be used carefully because batching improves throughput at the cost of per-record latency.

## Distributed Execution Model

Pipelinez now has an explicit distributed execution model for transport-backed sources.

### Host Options

Distributed behavior is enabled through `UseHostOptions(...)` and `PipelineHostOptions`.

Supported execution modes:

- `SingleProcess`
- `Distributed`

The runtime resolves and stores:

- `ExecutionMode`
- `InstanceId`
- `WorkerId`

If a caller does not supply instance or worker identity, the runtime generates reasonable defaults so logs, status output, and event payloads still identify the active worker.

### Runtime Context

`Pipeline<T>.GetRuntimeContext()` returns a `PipelineRuntimeContext` containing:

- pipeline name
- execution mode
- instance ID
- worker ID
- currently owned transport partitions or leases

This gives host applications a simple way to understand what the current worker owns without having to parse transport-specific client objects.

### Distributed Source Contract

Sources that support distributed ownership implement `IDistributedPipelineSource<T>`.

The contract lets the core runtime:

- validate distributed-mode compatibility at build time
- surface current ownership through transport-agnostic `PipelinePartitionLease` objects
- keep the core runtime free of Kafka-specific public types

Kafka is the first source implementation that fully supports this model.

### Distributed Events

The public event surface now includes worker lifecycle and rebalance hooks:

- `OnWorkerStarted`
- `OnPartitionsAssigned`
- `OnPartitionsRevoked`
- `OnWorkerStopping`

These events carry `PipelineRuntimeContext` and lease data so host applications can log worker startup, assignment changes, drain behavior, and shutdown explicitly.

### Record-Level Distribution Context

`OnPipelineRecordCompleted` and `OnPipelineRecordFaulted` now include `PipelineRecordDistributionContext`.

For Kafka-backed distributed execution, that context includes:

- instance ID
- worker ID
- transport name
- lease ID
- partition key
- partition ID
- offset

This makes per-record ownership and replay diagnostics available directly to consumers without forcing them to inspect raw metadata collections.

## Fault Handling And Error Policies

Fault handling is now a first-class part of the runtime.

### Fault State

When a source, segment, destination, or pipeline-level operation faults, the runtime captures a `PipelineFaultState` containing:

- the exception
- the component name
- the component kind
- the timestamp
- a human-readable message

Segment-level execution history is preserved separately in `SegmentHistory`.

### Events

The pipeline exposes:

- `OnPipelineRecordCompleted`
  raised after a record successfully completes the entire pipeline
- `OnPipelineRecordFaulted`
  raised when a record faults and enters policy handling
- `OnPipelineFaulted`
  raised when the pipeline transitions into a faulted runtime state

There is also an internal container-completed event used by integrations such as Kafka offset storage.

### Error Handler

The builder supports `WithErrorHandler(...)` with sync or async handlers. The handler receives `PipelineErrorContext<T>`, which includes:

- the exception
- the `PipelineContainer<T>`
- the captured `PipelineFaultState`
- the runtime cancellation token

The handler returns a `PipelineErrorAction`:

- `SkipRecord`
  skip the faulted record and continue processing
- `StopPipeline`
  mark the pipeline faulted and stop processing
- `Rethrow`
  mark the pipeline faulted and surface the original exception path

If no handler is configured, the default behavior is to stop the pipeline on fault.

## Status And Observability

`Pipeline<T>.GetStatus()` returns a `PipelineStatus` composed of `PipelineComponentStatus` entries for:

- the source
- each segment
- the destination

Reported execution status is derived from task state and runtime fault state:

- `Healthy`
- `Completed`
- `Faulted`
- `Unknown`

`PipelineStatus` now also carries `DistributedStatus`, which includes:

- execution mode
- instance ID
- worker ID
- currently owned partitions or leases

For distributed sources, this status reflects live ownership while the worker is active. For Kafka specifically, owned partitions are cleared on shutdown when the consumer leaves the group and revocation is observed.

Logging is managed through the internal `LoggingManager`, which wraps an `ILoggerFactory`. If the caller never supplies a logger factory, the runtime falls back to a null logger factory.
The runtime now also exposes additive performance metrics through `GetPerformanceSnapshot()` rather than relying only on logs for throughput diagnostics.

## Kafka Integration

Kafka support lives in the separate `Pipelinez.Kafka` assembly under `src/Pipelinez.Kafka/Kafka`.

### Builder Surface

Kafka extends the builder through `KafkaPipelineBuilderExtensions`:

- `WithKafkaSource(...)`
- `WithKafkaDestination(...)`

This keeps `PipelineBuilder<T>` owned by the core assembly and keeps Kafka-specific construction behavior owned by the Kafka assembly.

### Kafka Source

`KafkaPipelineSource<T, TRecordKey, TRecordValue>`:

- creates a Kafka consumer via `KafkaClientFactory`
- subscribes to the configured topic
- consumes messages in a loop
- maps Kafka key/value pairs into a pipeline record
- copies Kafka headers into `PipelineRecord.Headers`
- stores source topic, partition, and offset in container metadata
- maps topic/partition ownership into `PipelinePartitionLease` values
- reports partition assignment and revocation back into the core runtime
- populates record-level distribution metadata for completed and faulted events

When a record completes successfully, the source handles the internal container-completed event and stores the next Kafka offset.

Important consumer behavior:

- `EnableAutoCommit = true`
- `EnableAutoOffsetStore = false`

So completion is tied to explicit offset storage rather than immediate consume-time storage.
The Kafka consumer now relies on the broker's normal consumer-group offset behavior: committed offsets are resumed when they exist, while `StartOffsetFromBeginning` controls the `AutoOffsetReset` behavior for new groups without stored offsets.

### Kafka Destination

`KafkaPipelineDestination<T, TRecordKey, TRecordValue>`:

- creates a Kafka producer
- maps a pipeline record into a Kafka `Message<TKey, TValue>`
- ensures message headers exist
- copies pipeline headers into Kafka headers
- awaits `ProduceAsync(...)`

The destination only treats the record as complete after broker delivery has been awaited successfully.

### Configuration

Kafka configuration types include:

- `KafkaOptions`
- `KafkaSourceOptions`
- `KafkaDestinationOptions`
- `KafkaSchemaRegistryOptions`

The Kafka config path now supports both:

- secured SASL-based broker connections
- plain local broker connections for Docker-backed integration tests

Schema-registry-backed JSON and Avro serializer/deserializer configuration remains part of the public Kafka surface.

## Examples

### `Example.Kafka`

Demonstrates:

- configuring logging with Serilog
- building a pipeline with Kafka source and destination
- adding a custom segment
- awaiting pipeline startup and completion correctly
- observing successful completion through `OnPipelineRecordCompleted`

### `Example.Kafka.DataGen`

Provides a minimal Kafka producer for generating example traffic.

## Test Coverage

The solution now includes two test layers.

### Core Tests

`src/tests/Pipelinez.Tests` covers:

- pipeline construction
- startup and completion lifecycle guards
- segment ordering and mutation
- async destination behavior
- fault tracking and pipeline fault events
- error-handler policies
- logger integration
- builder-surface expectations
- performance-option propagation and override precedence
- runtime performance snapshot behavior
- batched destination execution
- segment parallelism behavior under configured throughput settings

### Kafka Integration Tests

`src/tests/Pipelinez.Kafka.Tests` uses Docker and `Testcontainers.Kafka` to run broker-backed integration tests that validate:

- source-topic to destination-topic flow
- header propagation through Kafka and the pipeline runtime
- segment fault handling with `SkipRecord`, `StopPipeline`, and `Rethrow`
- destination fault handling
- record-fault and pipeline-fault event behavior
- offset commit and replay behavior across pipeline runs
- distributed worker startup, rebalance, and shutdown behavior across multiple pipeline instances
- record-level worker and partition context on successful and faulted records
- partition reassignment when one distributed worker leaves the consumer group

At the time of this overview update, `dotnet test src\\Pipelinez.sln` passes with both the core and Kafka integration suites green.

## Current State

The major architectural work called out in the earlier planning docs has been implemented:

- async pipeline startup and guarded lifecycle semantics
- container-level fault state and segment execution history
- configurable error-handler policies
- async destination execution
- Kafka builder consolidation through extension methods
- nullability cleanup in production code
- Kafka split into a real `Pipelinez.Kafka` assembly
- Docker-backed Kafka integration tests
- explicit distributed execution mode with worker identity, partition ownership, and rebalance events
- explicit performance tuning controls, built-in performance snapshots, destination batching, and a benchmark project

The remaining work is mostly future evolution work rather than foundational cleanup. Likely areas include broader transport coverage, schema-registry integration tests, and further runtime ergonomics.

## Mental Model For Future Readers

The simplest way to think about Pipelinez is:

1. define a record type by inheriting from `PipelineRecord`
2. choose where records come from
3. chain one or more `PipelineSegment<T>` transforms
4. choose where processed records end up
5. optionally configure fault policy through `WithErrorHandler(...)`
6. observe success or failure through the public pipeline events

Under the hood, Pipelinez is a thin framework over TPL Dataflow that standardizes:

- record wrapping
- metadata flow
- fault capture
- completion semantics
- logging
- transport-specific adapters such as Kafka
