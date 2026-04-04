# Pipelinez

Typed data pipelines for .NET.

`Pipelinez` is the core runtime package. It provides:

- typed records
- sources, segments, and destinations
- async startup and completion
- fault handling and error policies
- retry
- dead-lettering
- flow control
- distributed execution abstractions
- performance and operational tooling

## Package Layout

- `Pipelinez`
  core runtime
- `Pipelinez.Kafka`
  Kafka transport extensions

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

- Repository: https://github.com/KenBerg75/Pipelinez
- Docs: https://github.com/KenBerg75/Pipelinez/tree/main/docs
