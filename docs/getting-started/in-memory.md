# In-Memory Pipeline

Audience: application developers evaluating Pipelinez for the first time.

## What This Covers

This guide walks through the smallest useful Pipelinez setup:

- define a record
- add a segment
- build an in-memory pipeline
- start it
- publish a record
- complete it cleanly

## Prerequisites

- .NET 8 SDK
- a local clone of this repository

Pipeline packaging and distribution are tracked separately, so the current path is source-first.

## Build The Solution

From the repository root:

```bash
dotnet build src/Pipelinez.sln
```

## Minimal Example

```csharp
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
    public required decimal Total { get; set; }
}

public sealed class NormalizeOrderSegment : PipelineSegment<OrderRecord>
{
    public override Task<OrderRecord> ExecuteAsync(OrderRecord record)
    {
        record.Total = decimal.Round(record.Total, 2);
        return Task.FromResult(record);
    }
}

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .AddSegment(new NormalizeOrderSegment(), new object())
    .WithInMemoryDestination("in-memory")
    .Build();

pipeline.OnPipelineRecordCompleted += (_, args) =>
{
    Console.WriteLine($"{args.Record.Id} completed with total {args.Record.Total}");
};

await pipeline.StartPipelineAsync();
await pipeline.PublishAsync(new OrderRecord { Id = "A-100", Total = 42.155m });
await pipeline.CompleteAsync();
await pipeline.Completion;
```

## What Happens

1. `Pipeline<T>.New("orders")` creates a `PipelineBuilder<T>`.
2. `WithInMemorySource(...)` adds a manual source that accepts `PublishAsync(...)`.
3. `AddSegment(...)` inserts a transform step.
4. `WithInMemoryDestination(...)` adds a sink.
5. `Build()` validates the pipeline and links the runtime graph.
6. `StartPipelineAsync()` activates the runtime.
7. `PublishAsync(...)` sends a record into the source.
8. `CompleteAsync()` stops accepting new work and lets in-flight records finish.
9. `Completion` completes when the full pipeline run is done.

## Important Behaviors

- `PublishAsync(...)` before `StartPipelineAsync()` throws.
- `CompleteAsync()` before `StartPipelineAsync()` throws.
- calling `StartPipelineAsync()` twice throws.
- awaiting `Completion` is the safest way to know all downstream work is done.

## Next Steps

- read [Lifecycle](../guides/lifecycle.md)
- read [Error Handling](../guides/error-handling.md)
- read [Performance](../guides/performance.md)
- read [Kafka Pipeline](kafka.md) when you want to move beyond in-memory flow
