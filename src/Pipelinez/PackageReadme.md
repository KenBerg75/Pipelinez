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

## When To Use This Package

Use `Pipelinez` when your application needs a typed pipeline runtime around record-processing logic and you do not want to hand-wire TPL Dataflow blocks, retry behavior, dead-letter handling, backpressure, metrics, and health reporting for every pipeline.

## Related Packages

- `Pipelinez`
  core runtime
- `Pipelinez.Kafka`
  Kafka transport extensions
- `Pipelinez.PostgreSql`
  PostgreSQL destination and dead-letter transport extensions

## Install

```bash
dotnet add package Pipelinez
```

## Quick Example

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
```

## More Information

- NuGet: https://www.nuget.org/packages/Pipelinez
- Repository: https://github.com/KenBerg75/Pipelinez
- Docs: https://github.com/KenBerg75/Pipelinez/tree/main/docs
