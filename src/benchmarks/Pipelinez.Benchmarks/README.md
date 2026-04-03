# Pipelinez.Benchmarks

This project contains the BenchmarkDotNet-based performance benchmarks for Pipelinez.

## Prerequisites

- .NET 8 SDK installed
- run benchmarks in `Release` mode for meaningful results

## Run All Benchmarks

From the repository root:

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks
```

## Run A Specific Benchmark

Use a BenchmarkDotNet filter:

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*InMemoryPipelineBenchmarks*"
```

You can also filter down to a specific benchmark method:

```bash
dotnet run -c Release --project src/benchmarks/Pipelinez.Benchmarks -- --filter "*TunedParallelPipeline*"
```

## What The Benchmarks Measure

The current benchmark suite focuses on in-memory pipeline execution and compares:

- default runtime settings
- tuned segment parallelism settings

BenchmarkDotNet reports metrics such as:

- mean execution time
- memory allocations
- throughput-oriented comparisons between benchmark cases

## Output

BenchmarkDotNet writes generated artifacts and reports under the benchmark project's output folders.

Look under:

- `src/benchmarks/Pipelinez.Benchmarks/bin/Release/net8.0/`
- `src/benchmarks/Pipelinez.Benchmarks/BenchmarkDotNet.Artifacts/`

## Notes

- Avoid running benchmarks while the machine is under heavy load.
- Prefer running the same command multiple times when comparing changes.
- Benchmark results are for relative comparison and regression detection, not absolute guarantees across machines.
