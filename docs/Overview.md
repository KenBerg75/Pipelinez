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
- `UseLogger(...)`
- `WithErrorHandler(...)`

Kafka integrates through extension methods in `Pipelinez.Kafka`, not through partial builder types. The Kafka assembly adds:

- `WithKafkaSource(...)`
- `WithKafkaDestination(...)`

`Build()` validates that a source and destination exist, creates a `Pipeline<T>`, links all blocks, and initializes the source and destination.

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

### Destination Behavior

Destinations derive from `PipelineDestination<T>`, which owns a `BufferBlock<PipelineContainer<T>>`.

The destination loop:

1. receives completed containers from upstream
2. checks for pre-faulted containers
3. delegates fault-policy decisions back to the pipeline when needed
4. executes `ExecuteAsync(T record, CancellationToken cancellationToken)` for successful containers
5. raises container-completed and record-completed events only after successful destination execution

Destination execution is fully async, and destination `Completion` now represents the full destination work lifecycle rather than only message-buffer completion.

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

Logging is managed through the internal `LoggingManager`, which wraps an `ILoggerFactory`. If the caller never supplies a logger factory, the runtime falls back to a null logger factory.

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

When a record completes successfully, the source handles the internal container-completed event and stores the next Kafka offset.

Important consumer behavior:

- `EnableAutoCommit = true`
- `EnableAutoOffsetStore = false`

So completion is tied to explicit offset storage rather than immediate consume-time storage.

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

### Kafka Integration Tests

`src/tests/Pipelinez.Kafka.Tests` uses Docker and `Testcontainers.Kafka` to run broker-backed integration tests that validate:

- source-topic to destination-topic flow
- header propagation through Kafka and the pipeline runtime
- segment fault handling with `SkipRecord`, `StopPipeline`, and `Rethrow`
- destination fault handling
- record-fault and pipeline-fault event behavior
- offset commit and replay behavior across pipeline runs

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
