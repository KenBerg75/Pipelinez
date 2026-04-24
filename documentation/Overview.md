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
  the main solution containing the core library, transport extension libraries, tests, and examples
- `src/Pipelinez`
  the transport-agnostic pipeline runtime
- `src/Pipelinez.Kafka`
  the Kafka transport extension assembly
- `src/Pipelinez.AzureServiceBus`
  the Azure Service Bus transport extension assembly
- `src/Pipelinez.RabbitMQ`
  the RabbitMQ transport extension assembly
- `src/Pipelinez.PostgreSql`
  the PostgreSQL destination and dead-letter transport extension assembly
- `src/Pipelinez.SqlServer`
  the SQL Server destination and dead-letter transport extension assembly
- `src/tests/Pipelinez.Tests`
  unit and runtime tests for core pipeline behavior
- `src/tests/Pipelinez.Kafka.Tests`
  Docker-backed Kafka integration tests using Testcontainers
- `src/tests/Pipelinez.AzureServiceBus.Tests`
  Azure Service Bus transport and approval tests
- `src/tests/Pipelinez.RabbitMQ.Tests`
  Docker-backed RabbitMQ transport and approval tests using Testcontainers
- `src/tests/Pipelinez.PostgreSql.Tests`
  Docker-backed PostgreSQL destination and dead-letter integration tests using Testcontainers
- `src/tests/Pipelinez.SqlServer.Tests`
  Docker-backed SQL Server destination and dead-letter integration tests using Testcontainers
- `src/benchmarks/Pipelinez.Benchmarks`
  BenchmarkDotNet project for repeatable in-memory and transport performance measurements
- `src/examples/Example.Kafka`
  sample application that builds a Kafka-backed pipeline
- `src/examples/Example.Kafka.DataGen`
  simple Kafka publisher used to generate example traffic
- `src/examples/Example.AzureServiceBus`
  sample application that builds an Azure Service Bus-backed pipeline
- `documentation/README.md`
  documentation index linking getting-started, guides, transport docs, operations docs, and architecture docs

## Packaging Status

The public packages are available on NuGet.org:

- [`Pipelinez`](https://www.nuget.org/packages/Pipelinez)
- [`Pipelinez.Kafka`](https://www.nuget.org/packages/Pipelinez.Kafka)
- [`Pipelinez.AzureServiceBus`](https://www.nuget.org/packages/Pipelinez.AzureServiceBus)
- [`Pipelinez.RabbitMQ`](https://www.nuget.org/packages/Pipelinez.RabbitMQ)
- [`Pipelinez.PostgreSql`](https://www.nuget.org/packages/Pipelinez.PostgreSql)
- [`Pipelinez.SqlServer`](https://www.nuget.org/packages/Pipelinez.SqlServer)

The repository remains configured for package metadata, XML docs, Source Link, symbol packages, and CI pack validation.
Public release automation is configured through tag-based GitHub Actions and NuGet Trusted Publishing.

Versioning rules:

- `Pipelinez`, `Pipelinez.Kafka`, `Pipelinez.AzureServiceBus`, `Pipelinez.RabbitMQ`, `Pipelinez.PostgreSql`, and `Pipelinez.SqlServer` ship with aligned versions
- stable releases use tags such as `v1.2.3`
- preview releases use tags such as `v1.3.0-preview.1`
- manual release workflow dispatch is intended for package validation and does not publish to NuGet.org

## Core Runtime Design

### Pipeline Construction

The entry point is `Pipeline<T>.New("name")`, which returns `PipelineBuilder<T>`.

The core builder currently supports:

- `WithSource(...)`
- `WithInMemorySource(...)`
- `AddSegment(...)`
- `WithDestination(...)`
- `WithInMemoryDestination(...)`
- `WithDeadLetterDestination(...)`
- `UseHostOptions(...)`
- `UseDeadLetterOptions(...)`
- `UseOperationalOptions(...)`
- `UseFlowControlOptions(...)`
- `UsePerformanceOptions(...)`
- `UseRetryOptions(...)`
- `UseLogger(...)`
- `WithErrorHandler(...)`

Kafka integrates through extension methods in `Pipelinez.Kafka`, not through partial builder types. The Kafka assembly adds:

- `WithKafkaSource(...)`
- `WithKafkaDestination(...)`
- `WithKafkaDeadLetterDestination(...)`

PostgreSQL also integrates through extension methods in `Pipelinez.PostgreSql`. The PostgreSQL assembly adds:

- `WithPostgreSqlDestination(...)`
- `WithPostgreSqlDeadLetterDestination(...)`

SQL Server also integrates through extension methods in `Pipelinez.SqlServer`. The SQL Server assembly adds:

- `WithSqlServerDestination(...)`
- `WithSqlServerDeadLetterDestination(...)`

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
- `RetryHistory`
  ordered `PipelineRetryAttempt` entries that capture retry failures and scheduled delays before terminal success or exhaustion

This container is the runtime boundary object shared by sources, segments, destinations, error handling, and transport adapters.

### Source Behavior

Sources derive from `PipelineSourceBase<T>`, which owns a `BufferBlock<PipelineContainer<T>>`.

Responsibilities:

- publish records manually through `PublishAsync`
- publish records with explicit flow-control behavior through `PublishAsync(..., PipelinePublishOptions)`
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
2. the segment executes `ExecuteAsync(T arg)` under the configured retry policy, if one exists
3. retry attempts are appended to `RetryHistory` and retry events are raised before each delayed retry
4. the returned record replaces `PipelineContainer<T>.Record`
5. the segment appends a `PipelineSegmentExecution` entry to `SegmentHistory`
6. if execution still fails after retries are exhausted, the segment marks the container faulted and allows downstream error handling to decide what to do

This keeps the segment authoring model simple while still preserving runtime-level observability.
Segments now also support configurable `DegreeOfParallelism`, `BoundedCapacity`, and `EnsureOrdered` behavior through `PipelineExecutionOptions`.

### Destination Behavior

Destinations derive from `PipelineDestination<T>`, which owns a `BufferBlock<PipelineContainer<T>>`.

The destination loop:

1. receives completed containers from upstream
2. checks for pre-faulted containers
3. delegates fault-policy decisions back to the pipeline when needed
4. executes `ExecuteAsync(T record, CancellationToken cancellationToken)` or batch execution for successful containers under the configured retry policy
5. records retry attempts on the container when destination execution fails transiently
6. raises container-completed and record-completed events only after successful destination execution

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
- `PublishAsync(record, PipelinePublishOptions)` returns `PipelinePublishResult`
- starting twice throws
- publishing before start throws
- completing before start throws
- `Completion` represents the pipeline run, not just one internal block
- `CompleteAsync()` waits for downstream destination work before final completion
- if the pipeline faults, `OnPipelineFaulted` is raised before `Completion` faults

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
- total retry count
- successful retry recovery count
- retry exhaustion count
- total dead-lettered count
- total dead-letter failure count
- total publish wait count
- average publish wait duration
- total publish rejection count
- peak buffered count
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

## Operational Tooling Model

Pipelinez now has a first-class operational surface layered on top of the existing status, eventing, and performance model.

### Operational Options

`UseOperationalOptions(...)` configures:

- health-check enablement
- meter-based metrics enablement
- correlation ID stamping
- degraded-state thresholds for saturation, retries, dead-lettering, publish rejection, and partition draining

### Health Status

`Pipeline<T>.GetHealthStatus()` returns a `PipelineHealthStatus` with:

- `PipelineName`
- `State`
- `Reasons`
- `ObservedAtUtc`
- current `PipelineStatus`
- current `PipelinePerformanceSnapshot`
- the last pipeline-level fault when one exists

Supported health states are:

- `Starting`
- `Healthy`
- `Degraded`
- `Unhealthy`
- `Completed`

### Operational Snapshot

`Pipeline<T>.GetOperationalSnapshot()` returns a `PipelineOperationalSnapshot` that combines:

- `PipelineStatus`
- `PipelinePerformanceSnapshot`
- `PipelineHealthStatus`
- last pipeline fault
- last successful completion timestamp
- last dead-letter timestamp

This gives hosts a single operator-oriented read model without replacing the existing lower-level APIs.

### Health Check Integration

Pipelinez now includes `PipelineHealthCheck<T>`, which implements `IHealthCheck` and maps runtime health into the standard .NET health-check model.

This allows ASP.NET Core and worker-service hosts to expose pipeline health through standard endpoints such as `/health`.

### Meter-Based Metrics

Pipelinez now emits runtime metrics through the `Pipelinez.Runtime` meter.

Current instruments include counters for:

- published records
- completed records
- faulted records
- retry attempts
- retry recoveries
- retry exhaustions
- dead-letter writes
- dead-letter failures
- publish rejections

It also exposes observable gauges for:

- current buffered record count
- owned partition count
- current records-per-second
- current health-state value

### Correlation IDs And Diagnostics

Pipelinez now stamps a correlation ID into record metadata when a record first enters the pipeline, unless one already exists.

The metadata key is:

- `pipelinez.correlation.id`

Correlation context is then surfaced through `PipelineRecordDiagnosticContext` on:

- `OnPipelineRecordCompleted`
- `OnPipelineRecordFaulted`
- `OnPipelineRecordRetrying`
- `OnPublishRejected`
- `OnPipelineRecordDeadLettered`
- `OnPipelineDeadLetterWriteFailed`

This makes it easier to correlate logs, faults, retries, and dead-letter outcomes back to one logical record flow.

### Destination Batching

Batch-capable destinations can implement `IBatchedPipelineDestination<T>`.

When batching is enabled:

- records are accumulated up to the configured batch size
- the batch is flushed early when the max batch delay is reached
- remaining records are flushed on normal completion
- completion events are only raised after the batch succeeds
- batch failures are converted into per-record fault handling so the existing error-policy model remains in control

This is intended for throughput-oriented destinations and should be used carefully because batching improves throughput at the cost of per-record latency.

## Flow Control Model

Pipelinez now has an explicit flow-control model layered on top of component bounded capacities.

### Flow Control Configuration

Flow behavior is configured through `UseFlowControlOptions(...)`.

`PipelineFlowControlOptions` controls:

- `OverflowPolicy`
- `PublishTimeout`
- `EmitSaturationEvents`
- `SaturationWarningThreshold`

Per-call overrides can then be supplied through `PublishAsync(record, PipelinePublishOptions)`.

Supported overflow behaviors are:

- `Wait`
- `Reject`
- `Cancel`

### Publish Semantics

Manual publishing now has two shapes:

- `PublishAsync(T record)`
- `PublishAsync(T record, PipelinePublishOptions options)`

The overload with options returns `PipelinePublishResult`, which tells the caller whether the record was accepted and, if not, whether it was rejected, timed out, or canceled.

The convenience overload still exists for callers that want exception-based behavior.

### Flow Status And Events

`PipelineStatus` now carries `FlowControlStatus`, which exposes:

- current overflow policy
- pipeline saturation state
- saturation ratio
- total buffered count
- total bounded capacity
- per-component queue depth and saturation

The public event surface now also includes:

- `OnSaturationChanged`
- `OnPublishRejected`

This makes pressure visible as operating state instead of only as a side effect of blocked tasks.

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
- current partition execution state for distributed-capable sources

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
- `OnPartitionDraining`
- `OnPartitionDrained`
- `OnPartitionExecutionStateChanged`
- `OnWorkerStopping`

These events carry `PipelineRuntimeContext` and lease data so host applications can log worker startup, assignment changes, drain behavior, and shutdown explicitly.

For partition-aware Kafka execution, the runtime now also exposes `PipelinePartitionExecutionState` values so hosts can inspect whether a partition is assigned, draining, currently in flight, and what the highest completed offset is for that worker.

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

## Retry Model

Pipelinez now has a first-class retry model for transient failures in segments and destinations.

### Retry Configuration

Retry behavior is configured through `UseRetryOptions(...)` and component-level overloads on the builder.

`PipelineRetryOptions<T>` can provide:

- `DefaultSegmentPolicy`
- `DestinationPolicy`
- `EmitRetryEvents`

Component-specific policies can override those defaults when calling `AddSegment(...)` or `WithDestination(...)`.

The built-in policy factories are:

- `PipelineRetryPolicy<T>.None()`
- `PipelineRetryPolicy<T>.FixedDelay(...)`
- `PipelineRetryPolicy<T>.ExponentialBackoff(...)`

Policies can then be narrowed with exception filters using `Handle<TException>()` or `Handle(...)`.

### Retry Execution

Retry execution is handled by a shared internal retry executor rather than duplicated independently in each component type.

For each retry-aware execution path:

1. the component attempts the operation
2. the policy evaluates whether the exception is retryable
3. a `PipelineRetryAttempt` is appended to `RetryHistory`
4. `OnPipelineRecordRetrying` is raised when retry events are enabled
5. the runtime waits for the configured delay, honoring pipeline cancellation
6. the operation is attempted again
7. if retries are exhausted, normal fault handling begins

If a later attempt succeeds, the container is not marked faulted and normal processing continues.

### Retry Observability

Retry behavior is visible through:

- `PipelineContainer<T>.RetryHistory`
- `OnPipelineRecordRetrying`
- retry counters in `PipelinePerformanceSnapshot`

This makes the difference between transient recovery and terminal failure visible in both event handlers and runtime diagnostics.

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

- `OnPipelineRecordRetrying`
  raised when a record is scheduled for another attempt after a retryable failure
- `OnSaturationChanged`
  raised when the pipeline crosses or clears the configured saturation warning threshold or full saturation state
- `OnPublishRejected`
  raised when a publish request is not accepted because it timed out, was canceled, or was rejected by overflow policy
- `OnPipelineRecordCompleted`
  raised after a record successfully completes the entire pipeline
- `OnPipelineRecordFaulted`
  raised when a record faults and enters policy handling
- `OnPipelineRecordDeadLettered`
  raised when a faulted record is preserved through the configured dead-letter path
- `OnPipelineDeadLetterWriteFailed`
  raised when a dead-letter write attempt fails
- `OnPipelineFaulted`
  raised when the pipeline transitions into a faulted runtime state

There is also an internal container-completed event used by integrations such as Kafka offset storage.

### Error Handler

The builder supports `WithErrorHandler(...)` with sync or async handlers. The handler receives `PipelineErrorContext<T>`, which includes:

- the exception
- the `PipelineContainer<T>`
- the captured `PipelineFaultState`
- `RetryHistory`
- `RetryAttemptCount`
- `RetryExhausted`
- the runtime cancellation token

The handler returns a `PipelineErrorAction`:

- `SkipRecord`
  skip the faulted record and continue processing
- `DeadLetter`
  preserve the faulted record through the configured dead-letter destination and continue if the dead-letter write succeeds
- `StopPipeline`
  mark the pipeline faulted and stop processing
- `Rethrow`
  mark the pipeline faulted and surface the original exception path

If no handler is configured, the default behavior is to stop the pipeline on fault.
Retries always run before the error handler is invoked. If retry eventually succeeds, the error handler is never called. If retry is exhausted, the handler receives the populated retry context and can decide whether to skip, stop, or rethrow.

### Dead-Letter Model

Dead-lettering is now a first-class terminal record outcome.

When a handler returns `DeadLetter`, the runtime:

1. builds a `PipelineDeadLetterRecord<T>`
2. copies the original record, fault state, metadata, segment history, retry history, and distribution context
3. writes that envelope to the configured `IPipelineDeadLetterDestination<T>`
4. treats the record as terminally handled without raising the normal completion event

Dead-letter writes are available through:

- `InMemoryDeadLetterDestination<T>` in core
- `WithKafkaDeadLetterDestination(...)` in `Pipelinez.Kafka`

Dead-letter write failures are explicit:

- by default they fault the pipeline
- they raise `OnPipelineDeadLetterWriteFailed`
- they increment dead-letter failure counters in `PipelinePerformanceSnapshot`

The dead-letter path does not require special removal from TPL Dataflow blocks. Faulted containers already leave the active block as part of normal execution and then reach the destination-side terminal fault handler, where the dead-letter decision is applied.

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
- current partition execution state

For distributed sources, this status reflects live ownership while the worker is active. For Kafka specifically, owned partitions are cleared on shutdown when the consumer leaves the group and revocation is observed.

`PipelineStatus` now also carries `FlowControlStatus`, which exposes queue pressure and bounded-capacity saturation across the source, segments, and destination.

Logging is managed through the internal `LoggingManager`, which wraps an `ILoggerFactory`. If the caller never supplies a logger factory, the runtime falls back to a null logger factory.
The runtime now also exposes additive performance metrics through `GetPerformanceSnapshot()` rather than relying only on logs for throughput diagnostics, including retry counts, retry recovery/exhaustion totals, publish wait totals, publish rejection totals, and peak buffered depth.
Those same operational signals can now also flow through the `Pipelinez.Runtime` meter and the health/operational snapshot APIs.

## Kafka Integration

Kafka support lives in the separate `Pipelinez.Kafka` assembly under `src/Pipelinez.Kafka/Kafka`.

### Builder Surface

Kafka extends the builder through `KafkaPipelineBuilderExtensions`:

- `WithKafkaSource(...)`
- `WithKafkaDestination(...)`
- `WithKafkaDeadLetterDestination(...)`

Kafka source configuration can now also include `KafkaPartitionScalingOptions`, either by setting `KafkaSourceOptions.PartitionScaling` directly or by using the overload that accepts partition scaling explicitly.

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
- tracks partition-local execution state and in-flight work
- applies partition-aware pause/resume behavior based on `KafkaPartitionScalingOptions`
- reports partition assignment and revocation back into the core runtime
- raises partition drain and partition execution-state events through the pipeline runtime
- populates record-level distribution metadata for completed and faulted events

When a record completes successfully, the source handles the internal container-completed event and stores the next Kafka offset.
When partition-aware scaling is enabled, Kafka offset advancement is tracked per partition so contiguous completion can be stored safely even when within-partition ordering is intentionally relaxed.

Important consumer behavior:

- `EnableAutoCommit = true`
- `EnableAutoOffsetStore = false`

So completion is tied to explicit offset storage rather than immediate consume-time storage.
The Kafka consumer now relies on the broker's normal consumer-group offset behavior: committed offsets are resumed when they exist, while `StartOffsetFromBeginning` controls the `AutoOffsetReset` behavior for new groups without stored offsets.

### Kafka Partition-Aware Scaling

Kafka now has an explicit partition-aware scaling model.

`KafkaPartitionScalingOptions` controls:

- `ExecutionMode`
- `MaxConcurrentPartitions`
- `MaxInFlightPerPartition`
- `RebalanceMode`
- `EmitPartitionExecutionEvents`

Supported execution modes are:

- `PreservePartitionOrder`
- `ParallelizeAcrossPartitions`
- `RelaxOrderingWithinPartition`

The default is to preserve ordering within a partition while still allowing concurrency across independently owned partitions. Relaxing ordering within a partition is now explicit and opt-in.

### Kafka Destination

`KafkaPipelineDestination<T, TRecordKey, TRecordValue>`:

- creates a Kafka producer
- maps a pipeline record into a Kafka `Message<TKey, TValue>`
- ensures message headers exist
- copies pipeline headers into Kafka headers
- awaits `ProduceAsync(...)`

The destination only treats the record as complete after broker delivery has been awaited successfully.

### Kafka Dead-Letter Destination

`KafkaDeadLetterDestination<T, TRecordKey, TRecordValue>`:

- reuses the existing Kafka producer infrastructure
- maps `PipelineDeadLetterRecord<T>` into a Kafka `Message<TKey, TValue>`
- copies pipeline headers into the dead-letter message
- adds dead-letter fault headers for component, component kind, and fault timestamp
- awaits broker acknowledgement before the runtime treats the dead-letter write as successful

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

## PostgreSQL Integration

PostgreSQL support lives in the separate `Pipelinez.PostgreSql` assembly under `src/Pipelinez.PostgreSql`.

### Builder Surface

PostgreSQL extends the builder through `PostgreSqlPipelineBuilderExtensions`:

- `WithPostgreSqlDestination(...)`
- `WithPostgreSqlDeadLetterDestination(...)`

This keeps `PipelineBuilder<T>` transport-agnostic while still exposing PostgreSQL-specific construction behavior where it belongs.

### PostgreSQL Destination

The PostgreSQL destination:

- maps a pipeline record either through `PostgreSqlTableMap<T>` or a custom `PostgreSqlCommandDefinition`
- generates parameterized `INSERT` statements for table-map-backed writes
- executes commands through Dapper on top of `Npgsql`
- only completes the record after PostgreSQL acknowledges the write

The transport is intentionally schema-agnostic. Consumers choose the schema, table, and column names that make sense for their system.

### PostgreSQL Dead-Letter Destination

The PostgreSQL dead-letter destination supports two shapes:

- `PostgreSqlTableMap<PipelineDeadLetterRecord<T>>`
- `Func<PipelineDeadLetterRecord<T>, PostgreSqlCommandDefinition>`

That allows consumers to use either a simple mapped dead-letter table or a fully custom SQL write for audit/compliance scenarios.

### Configuration

PostgreSQL configuration supports:

- `ConnectionString`
- `ConfigureConnectionString`
- `ConfigureDataSource`
- externally supplied `NpgsqlDataSource`

This gives consumers full control over pooling and driver configuration while keeping Pipelinez responsible only for command execution.

## SQL Server Integration

SQL Server support lives in the separate `Pipelinez.SqlServer` assembly under `src/Pipelinez.SqlServer`.

The SQL Server transport is destination-focused. It writes successful records and dead-letter records to consumer-owned tables, and it does not provide SQL Server source, table queue polling, CDC, or change tracking support.

### Builder Surface

SQL Server extends the builder through `SqlServerPipelineBuilderExtensions`:

- `WithSqlServerDestination(...)`
- `WithSqlServerDeadLetterDestination(...)`

### SQL Server Destination

The SQL Server destination:

- maps a pipeline record either through `SqlServerTableMap<T>` or a custom `SqlServerCommandDefinition`
- generates parameterized `INSERT` statements for table-map-backed writes
- bracket-quotes mapped identifiers and escapes closing brackets
- executes commands through Dapper on top of `Microsoft.Data.SqlClient`
- only completes the record after SQL Server acknowledges the write

`MapJson(...)` serializes values as JSON text. Consumers can enforce JSON validity with `ISJSON(...)` constraints on `nvarchar(max)` columns.

### SQL Server Dead-Letter Destination

The SQL Server dead-letter destination supports two shapes:

- `SqlServerTableMap<PipelineDeadLetterRecord<T>>`
- `Func<PipelineDeadLetterRecord<T>, SqlServerCommandDefinition>`

### Configuration

SQL Server configuration supports:

- `ConnectionString`
- `ConfigureConnectionString`
- `CommandTimeoutSeconds`
- `SerializerOptions`

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
- flow-control wait, reject, cancel, status, and saturation event behavior
- retry policy behavior, retry events, retry exhaustion, and retry-aware error handling
- logger integration
- builder-surface expectations
- performance-option propagation and override precedence
- runtime performance snapshot behavior
- batched destination execution
- segment parallelism behavior under configured throughput settings

### Kafka Integration Tests

`src/tests/Pipelinez.Kafka.Tests` uses Docker and `Testcontainers.Kafka` to run broker-backed integration tests that validate:

- source-topic to destination-topic flow
- dead-letter topic publishing for faulted records
- header propagation through Kafka and the pipeline runtime
- segment fault handling with `SkipRecord`, `StopPipeline`, and `Rethrow`
- destination fault handling
- record-fault and pipeline-fault event behavior
- transient segment failures that recover under retry
- downstream pressure slowing Kafka-backed ingress safely without faulting the pipeline
- offset commit and replay behavior across pipeline runs
- distributed worker startup, rebalance, and shutdown behavior across multiple pipeline instances
- record-level worker and partition context on successful and faulted records
- partition reassignment when one distributed worker leaves the consumer group
- partition-local ordering by default and opt-in out-of-order completion when within-partition concurrency is enabled
- partition execution-state visibility in runtime context and distributed status

### PostgreSQL Integration Tests

`src/tests/Pipelinez.PostgreSql.Tests` uses Docker and `Testcontainers.PostgreSql` to validate:

- direct record-to-table mapping into consumer-owned schemas and tables
- custom parameterized SQL execution
- dead-letter table mapping
- dead-letter custom SQL execution
- option validation and generated SQL safety
- public API approval coverage for the PostgreSQL package

### SQL Server Integration Tests

`src/tests/Pipelinez.SqlServer.Tests` uses Docker and `Testcontainers.MsSql` to validate:

- direct record-to-table mapping into consumer-owned schemas and tables
- custom parameterized SQL execution
- dead-letter table mapping
- dead-letter custom SQL execution
- connection-string customization, bracket quoting, JSON text mapping, and generated SQL safety
- public API approval coverage for the SQL Server package

The standard validation path is `dotnet test src\\Pipelinez.sln`. Kafka, RabbitMQ, PostgreSQL, and SQL Server suites require Docker/Testcontainers, while Azure Service Bus live end-to-end coverage is opt-in through `PIPELINEZ_ASB_CONNECTION_STRING`.

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
- explicit partition-aware Kafka scaling with partition execution state, drain events, and per-partition scheduling rules
- explicit performance tuning controls, built-in performance snapshots, destination batching, and a benchmark project
- explicit retry policies with retry history, retry events, and retry-aware performance counters
- explicit dead-letter flows with in-memory and Kafka dead-letter destinations
- explicit PostgreSQL destination and dead-letter transport support
- explicit SQL Server destination and dead-letter transport support
- explicit flow-control policies with publish results, saturation status, and pressure metrics
- explicit operational tooling with health snapshots, health checks, meter metrics, and correlation-aware diagnostics
- explicit dependency and security automation with Dependabot, Dependency Review, CodeQL, OpenSSF Scorecard, and release SBOM generation

The remaining work is mostly future evolution work rather than foundational cleanup. Likely areas include broader transport coverage, schema-registry integration tests, and further runtime ergonomics.

## API Stability And Governance

Pipelinez now also has explicit public API governance in place.

- the repository treats `Pipelinez`, `Pipelinez.Kafka`, `Pipelinez.AzureServiceBus`, `Pipelinez.RabbitMQ`, `Pipelinez.PostgreSql`, and `Pipelinez.SqlServer` as intentional consumer contracts
- public API approval tests snapshot the compiled surface of all public package assemblies
- accidental API changes now fail the normal test suite, which means they are also caught by the existing PR and CI workflows
- contributor guidance now distinguishes stable, preview, and internal-only surface area

This does not freeze the project completely, but it does mean public API changes are now expected to be deliberate, reviewable, and documented.

## Mental Model For Future Readers

The simplest way to think about Pipelinez is:

1. define a record type by inheriting from `PipelineRecord`
2. choose where records come from
3. chain one or more `PipelineSegment<T>` transforms
4. choose where processed records end up
5. optionally configure flow control through `UseFlowControlOptions(...)`
6. optionally configure retry behavior through `UseRetryOptions(...)`
7. optionally configure fault policy through `WithErrorHandler(...)`
8. observe pressure, retry, success, or failure through the public pipeline events

Under the hood, Pipelinez is a thin framework over TPL Dataflow that standardizes:

- record wrapping
- metadata flow
- fault capture
- flow control
- retry execution
- completion semantics
- logging
- transport-specific adapters such as Kafka
