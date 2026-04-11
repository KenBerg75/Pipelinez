# Pipelinez

Typed, observable record-processing pipelines for .NET.

`Pipelinez` is the core runtime package for building source -> segment -> destination pipelines inside normal .NET applications. Use it when you need to ingest, enrich, transform, route, or publish records with consistent lifecycle management, retries, dead-lettering, flow control, and observability.

## What This Package Does

`Pipelinez` provides:

- typed records
- sources, segments, and destinations
- async startup and completion
- fault handling and error policies
- retry
- dead-lettering
- flow control
- distributed execution abstractions
- performance and operational tooling

## Install

```bash
dotnet add package Pipelinez
```

## When To Use This Package

Use `Pipelinez` when your application needs a typed pipeline runtime around record-processing logic and you do not want to hand-wire TPL Dataflow blocks, retry behavior, dead-letter handling, backpressure, metrics, and health reporting for every pipeline.

## Minimal Example

```csharp
using Pipelinez.Core;
using Pipelinez.Core.Record;

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
}

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithInMemoryDestination("in-memory")
    .Build();

await pipeline.StartPipelineAsync();
await pipeline.PublishAsync(new OrderRecord { Id = "A-100" });
await pipeline.CompleteAsync();
await pipeline.Completion;
```

## Common Recipes

- Build an in-memory pipeline with typed records.
- Add retry policies before terminal error handling.
- Dead-letter permanently failed records.
- Expose pipeline health, metrics, and performance snapshots.

## Related Packages

- [`Pipelinez.Kafka`](https://www.nuget.org/packages/Pipelinez.Kafka)
  Kafka source, destination, dead-lettering, distributed execution, and partition-aware scaling.
- [`Pipelinez.PostgreSql`](https://www.nuget.org/packages/Pipelinez.PostgreSql)
  PostgreSQL destination and dead-letter writes.

## Documentation

- NuGet: https://www.nuget.org/packages/Pipelinez
- Repository: https://github.com/KenBerg75/Pipelinez
- API reference: https://kenberg75.github.io/Pipelinez/api/Pipelinez.Core.html
- Getting started: https://github.com/KenBerg75/Pipelinez/blob/main/documentation/getting-started/in-memory.md
- Docs: https://github.com/KenBerg75/Pipelinez/tree/main/documentation
