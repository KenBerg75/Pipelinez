# Overview

## What Pipelinez Is

Pipelinez is a .NET 8 library for building typed data-processing pipelines around a small set of abstractions:

- `PipelineRecord`: the base type for any payload that moves through a pipeline.
- `IPipelineSource<T>`: publishes records into the flow.
- `IPipelineSegment<T>`: transforms records in the middle of the flow.
- `IPipelineDestination<T>`: consumes records at the end of the flow.

The runtime is built on `System.Threading.Tasks.Dataflow`. A pipeline is linked as:

`source -> segment 1 -> segment 2 -> ... -> destination`

Every record is wrapped in a `PipelineContainer<T>` so the framework can carry both the user-defined record and framework metadata together.

## Solution Layout

- `src/Pipelinez.sln`
  The main solution file. It includes the core library, tests, and two examples.
- `src/Pipelinez`
  The primary library project. This contains the core pipeline runtime and, at the moment, the Kafka implementation as well.
- `src/Pipelinez.Kafka`
  A separate project file intended for Kafka support, but it currently contains only `Pipelinez.Kafka.csproj` and is not included in the solution. The Kafka source files actually live under `src/Pipelinez/Kafka`, so Kafka functionality currently ships from the main `Pipelinez` assembly rather than a separate Kafka assembly.
- `src/tests/Pipelinez.Tests`
  xUnit tests covering pipeline construction, execution, segment behavior, ordering, and logger integration.
- `src/examples/Example.Kafka`
  A sample app showing the intended fluent builder experience for a Kafka-backed pipeline.
- `src/examples/Example.Kafka.DataGen`
  A simple utility for publishing test data to Kafka.

## Core Runtime Design

### 1. Pipeline construction

The entry point is `Pipeline<T>.New("name")`, which returns `PipelineBuilder<T>`.

The builder currently supports:

- `WithInMemorySource(...)`
- `AddSegment(...)`
- `WithInMemoryDestination(...)`
- `UseLogger(...)`
- Kafka builder extensions through a partial `PipelineBuilder<T>` in `src/Pipelinez/Kafka/PipelineBuilder.cs`

`Build()` validates that a source and destination exist, creates a `Pipeline<T>`, links all blocks together, and initializes the source and destination.

### 2. Record model

`PipelineRecord` is the base class for pipeline payloads. It only includes a list of `PipelineRecordHeader`, leaving record shape entirely up to consumers of the library.

Each record is wrapped in `PipelineContainer<T>`:

- `Record`: the typed payload being processed
- `Metadata`: a `MetadataCollection` used by framework integrations such as Kafka offset tracking

This wrapper is central to the design because pipeline infrastructure needs more than just the payload itself.

### 3. Source behavior

Sources inherit from `PipelineSourceBase<T>`, which owns a `BufferBlock<PipelineContainer<T>>`.

Responsibilities:

- accept manual publishes through `PublishAsync`
- optionally create records from an external system in `MainLoop(...)`
- link to the next pipeline component
- subscribe to pipeline completion events via `OnPipelineContainerComplete(...)`

`InMemoryPipelineSource<T>` is effectively a passive/manual source. Its main loop only waits until cancellation, and records enter the pipeline through `PublishAsync`.

### 4. Segment behavior

Segments inherit from `PipelineSegment<T>`, which wraps a `TransformBlock<PipelineContainer<T>, PipelineContainer<T>>`.

For each container:

1. the segment calls the user-defined `ExecuteAsync(T arg)`
2. the returned record is written back to `PipelineContainer<T>.Record`
3. the container continues to the next block

This gives segments a simple authoring model: developers work with their record type directly instead of with dataflow primitives.

### 5. Destination behavior

Destinations inherit from `PipelineDestination<T>`, which owns a `BufferBlock<PipelineContainer<T>>`.

The destination:

1. receives completed containers from the final upstream block
2. executes its destination-specific logic through `ExecuteAsync(T record)`
3. raises a pipeline completion event for the container and then for the record

`InMemoryPipelineDestination<T>` is a sink that only logs receipt of the record.

## Execution Lifecycle

The runtime lifecycle for a typical in-memory pipeline is:

1. Build the pipeline with a source, optional segments, and a destination.
2. Call `StartPipelineAsync(CancellationTokenSource)`.
3. Publish one or more records through `PublishAsync`.
4. Call `CompleteAsync()` to complete the source and wait for completion to propagate to the destination.
5. Optionally await `pipeline.Completion`.

When a record reaches the destination, the pipeline raises:

- an internal container-completed event for framework use
- a public `OnPipelineRecordCompleted` event for consumers

That public event is the main extension point for callers who want to observe successful record completion.

## Status and Observability

`Pipeline<T>.GetStatus()` returns a `PipelineStatus` composed of `PipelineComponentStatus` entries for:

- the source
- each segment
- the destination

Task states are translated into:

- `Healthy`
- `Completed`
- `Faulted`
- `Unknown`

Logging is managed through the internal singleton `LoggingManager`, which wraps an `ILoggerFactory`. If the caller never supplies a logger factory, the library falls back to `NullLoggerFactory`.

## Kafka Integration

Kafka support is implemented inside `src/Pipelinez/Kafka`.

### Source side

`KafkaPipelineSource<T, TRecordKey, TRecordValue>`:

- creates a Kafka consumer through `KafkaClientFactory`
- subscribes to a topic
- consumes Kafka messages in a loop
- maps Kafka key/value pairs into a pipeline record using a caller-provided mapper
- copies Kafka headers into `PipelineRecord.Headers`
- stores topic, partition, and offset metadata in the `PipelineContainer`

Once a record completes the pipeline, the Kafka source handles the internal completion event and stores the next offset with `_consumer.StoreOffset(...)`.

Important detail:

- consumer config sets `EnableAutoCommit = true`
- consumer config also sets `EnableAutoOffsetStore = false`

So the pipeline is trying to defer offset storage until the record has finished the pipeline, which is the right direction for "process then commit" behavior.

### Destination side

`KafkaPipelineDestination<T, TRecordKey, TRecordValue>`:

- creates a Kafka producer
- maps a pipeline record to a Kafka `Message<TKey, TValue>`
- copies `PipelineRecord.Headers` into Kafka headers
- produces to the configured topic

Schema-registry-backed JSON and Avro serializers/deserializers are supported through `KafkaSchemaRegistryOptions`.

### Factory and configuration

Kafka client creation is centralized in `KafkaClientFactory`, with separate wrappers for:

- admin client
- consumer
- producer

Configuration classes:

- `KafkaOptions`
- `KafkaSourceOptions`
- `KafkaDestinationOptions`
- `KafkaSchemaRegistryOptions`

These are straightforward POCOs that the caller supplies to the builder.

## Examples

### `Example.Kafka`

Demonstrates the intended fluent setup:

- configure logging with Serilog
- build a pipeline with a Kafka source
- add a custom segment (`GoogleSegment`)
- send the transformed record to a Kafka destination
- observe completion through `OnPipelineRecordCompleted`

`GoogleSegment` shows the expected programming model for custom segments: inherit `PipelineSegment<T>` and implement `ExecuteAsync`.

### `Example.Kafka.DataGen`

Publishes simple string messages to Kafka to generate test traffic for the example pipeline.

## Test Coverage

The current tests validate the main in-memory behavior:

- pipeline builds with source + destination
- pipeline builds with source + multiple segments + destination
- records can be published and observed through `OnPipelineRecordCompleted`
- multiple records flow through the pipeline
- segments mutate records as expected
- multiple segments execute in order
- logger integration is invoked
- null logger factories are rejected

All existing tests pass with `dotnet test src\\Pipelinez.sln`.

## Current Implementation Notes

The codebase is functional for its in-memory path and demonstrates the intended Kafka extension model, but several parts are still early-stage or incomplete:

- `StartPipelineAsync` is declared `async void` and does not await anything. It currently fires source and destination tasks and returns immediately.
- `WithErrorHandler(...)` is not implemented.
- the older string-based Kafka builder methods in `PipelineBuilder<T>` remain as `NotImplementedException` stubs, while the actual Kafka builder support lives in the partial builder under `src/Pipelinez/Kafka/PipelineBuilder.cs`.
- `PipelineDestination<T>.ExecuteAsync` is synchronous despite its name.
- several nullable warnings remain throughout the solution.
- error handling inside segments and destinations logs failures, but the fault-handling story is not fully implemented yet.
- the separate `Pipelinez.Kafka` project structure does not yet match the actual source layout.

## Mental Model For Future Readers

The simplest way to think about Pipelinez is:

1. define a record type by inheriting from `PipelineRecord`
2. choose where records come from
3. chain one or more `PipelineSegment<T>` transforms
4. choose where processed records end up
5. use the completion event to observe successful end-to-end processing

Under the hood, the library is a light framework over TPL Dataflow that standardizes record wrapping, metadata propagation, logging, and transport-specific adapters such as Kafka.
