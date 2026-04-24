# Performance

Audience: application developers tuning throughput and understanding runtime metrics.

## What This Covers

- `UsePerformanceOptions(...)`
- `PipelineExecutionOptions`
- batching
- `GetPerformanceSnapshot()`

## Configure Execution

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
        },
        DestinationBatching = new PipelineBatchingOptions
        {
            BatchSize = 250,
            MaxBatchDelay = TimeSpan.FromMilliseconds(50)
        }
    })
    .WithInMemorySource(new object())
    .AddSegment(new MySegment(), new object())
    .WithInMemoryDestination("config")
    .Build();
```

## Read A Snapshot

```csharp
var snapshot = pipeline.GetPerformanceSnapshot();

Console.WriteLine(snapshot.RecordsPerSecond);
Console.WriteLine(snapshot.TotalRecordsCompleted);
Console.WriteLine(snapshot.AverageEndToEndLatency);
```

## Important Tradeoffs

- increasing `DegreeOfParallelism` can improve throughput
- disabling `EnsureOrdered` can change observable completion order
- batching improves throughput at the cost of per-record latency
- flow-control and performance tuning work together, especially through bounded capacities

## Benchmarks

The repo includes a benchmark project:

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks
```

See [Benchmarking](benchmarking.md) for transport filters, environment gates, and artifact publication guidance.

## Related Docs

- [Flow Control](flow-control.md)
- [Operational Tooling](operational-tooling.md)
- [Benchmarking](benchmarking.md)
- [Benchmark README](https://github.com/KenBerg75/Pipelinez/blob/main/src/benchmarks/Pipelinez.Benchmarks/README.md)
